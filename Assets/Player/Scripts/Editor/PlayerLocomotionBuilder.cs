using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Rebuilds the player locomotion graph from scratch via Animator > Rebuild Player Locomotion.
// Authoring it in code rather than by hand keeps the blend thresholds, damping and transition
// settings reviewable in git, and makes a bad experiment one menu click away from being undone.
public static class PlayerLocomotionBuilder
{
    // One clip per compass direction, at one playback speed. A tier of the Speed axis is exactly
    // this, so passing it around as a set keeps the eight-argument version of BuildLocomotion away.
    private readonly struct StrafeSet
    {
        public readonly AnimationClip Forward;
        public readonly AnimationClip Backward;
        public readonly AnimationClip Left;
        public readonly AnimationClip Right;
        public readonly float Speed;

        public StrafeSet(AnimationClip forward, AnimationClip backward, AnimationClip left,
            AnimationClip right, float speed = 1.0f)
        {
            Forward = forward;
            Backward = backward;
            Left = left;
            Right = right;
            Speed = speed;
        }

        // A tier with a hole in it would blend toward an empty motion, freezing the character
        // whenever it faced that way, so a partial set is treated as no set at all.
        public bool IsComplete => Forward != null && Backward != null && Left != null && Right != null;

        public StrafeSet AtSpeed(float speed) => new StrafeSet(Forward, Backward, Left, Right, speed);
    }

    private const string ControllerPath = "Assets/Player/Art/PlayerAnimationController.controller";
    private const string AnimationsRoot = "Assets/Player/Art/Animations";
    private const string ArmMaskPath = "Assets/Player/Art/Animations/Crouching/CrouchArmsMask.mask";

    // PlayerAnimator finds these layers by name to drive their weights. Renaming them here means
    // renaming StandingArmsLayer and CrouchArmsLayer there.
    private const string StandingArmsLayer = "Standing Arms";
    private const string CrouchArmsLayer = "Crouch Arms";

    // Playback multiplier on the sprint tree until real sprint clips exist: reusing the walk
    // clips faster is the standard stopgap, and it keeps the graph shape final.
    private const float SprintClipSpeed = 1.45f;

    // PlayerAim reads the length of whichever state carries this tag, and paces the body's
    // rotation to it. Renaming the tag here means renaming TurnTag there.
    private const string TurnTag = "Turn";

    // Below this Speed the character counts as standing still, so turn-in-place is allowed.
    private const float StandingSpeed = 0.1f;

    // Speed above which a jump is treated as a running one. Half a step into the walk tier:
    // anything with real forward motion reads wrong as a standing jump.
    private const float RunningJumpSpeed = 0.5f;

    // Blend lengths into the single jump clip. The running jump snaps in, because the character
    // is already leaning. The standing jump eases in over a third of a second, which keeps the
    // idle pose through the crouch and takeoff and takes most of the run out of the clip.
    private const float RunningJumpBlend = 0.08f;
    private const float StandingJumpBlend = 0.32f;

    [MenuItem(MenuPaths.PlayerRebuildLocomotion, false, MenuPaths.PlayerPriority)]
    public static void Rebuild()
    {
        AnimationClip idle = LoadClip("Standing/standing_idle.fbx");
        AnimationClip walkForward = LoadClip("Walking/walking_forward.fbx");
        AnimationClip walkBackward = LoadClip("Walking/walking_backward.fbx");
        AnimationClip walkLeft = LoadClip("Walking/walking_left.fbx");
        AnimationClip walkRight = LoadClip("Walking/walking_right.fbx");
        AnimationClip crouchIdle = LoadClip("Crouching/crouching_idle.fbx");
        AnimationClip crouchForward = LoadClip("Crouching/crouching_walk_forward.fbx");
        AnimationClip crouchLeft = LoadClip("Crouching/crouching_walk_left.fbx");
        AnimationClip crouchRight = LoadClip("Crouching/crouching_walk_right.fbx");
        AnimationClip toCrouch = LoadClip("Standing/standing_trans_crouch.fbx");
        AnimationClip toStanding = LoadClip("Crouching/crouching_trans_standing.fbx");
        AnimationClip turnLeft = LoadClip("Standing/standing_left_turn.fbx");
        AnimationClip turnRight = LoadClip("Standing/standing_right_turn.fbx");
        AnimationClip jump = LoadClip("Running/running_jump.fbx");

        StrafeSet walk = new StrafeSet(walkForward, walkBackward, walkLeft, walkRight);
        StrafeSet run = new StrafeSet(
            LoadClip("Running/running_forward.fbx"),
            LoadClip("Running/running_backward.fbx"),
            LoadClip("Running/running_left_strafe.fbx"),
            LoadClip("Running/running_right_strafe.fbx"));

        if (idle == null || !walk.IsComplete)
        {
            Debug.LogError("[PlayerLocomotionBuilder] Missing a required locomotion clip. Aborting.");
            return;
        }

        if (!run.IsComplete)
            Debug.LogWarning("[PlayerLocomotionBuilder] Incomplete run clip set; the sprint tier " +
                             $"falls back to the walk clips at {SprintClipSpeed:0.00}x.");

        // Reset in place rather than delete and recreate: a new asset would get a new GUID and
        // silently unhook the Animator component on the player prefab.
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        else ResetController(controller);

        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Crouching", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        // -1, 0 or 1, written by PlayerAnimator from PlayerAim.TurnDirection.
        controller.AddParameter("Turn", AnimatorControllerParameterType.Float);
        // Fired by PlayerAnimator on the frame PlayerMotor launches a jump.
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine root = controller.layers[0].stateMachine;

        AnimatorState locomotion = BuildLocomotion(controller, idle, walk, run);
        locomotion.writeDefaultValues = false;
        root.defaultState = locomotion;

        // No crouch clip means no crouch state. An empty state would still be entered when
        // Crouching goes true and would freeze the rig mid-pose, which reads as a broken
        // character; staying in Locomotion is merely the wrong animation.
        if (crouchForward != null)
        {
            StrafeSet crouchStrafe = new StrafeSet(crouchForward, crouchForward, crouchLeft, crouchRight);
            AnimatorState crouch = BuildCrouch(controller, crouchIdle, crouchStrafe);
            crouch.writeDefaultValues = false;
            LayoutStates(root, locomotion, crouch);
            WireCrouchTransitions(root, locomotion, crouch, toCrouch, toStanding);
        }
        else
        {
            Debug.LogWarning("[PlayerLocomotionBuilder] No crouch clip; skipping the Crouch state. " +
                             "The player will keep using standing locomotion while crouched.");
            LayoutStates(root, locomotion, null);
        }

        if (turnLeft != null && turnRight != null) BuildTurns(root, locomotion, turnLeft, turnRight);
        else Debug.LogWarning("[PlayerLocomotionBuilder] Missing a standing turn clip; skipping " +
                              "turn-in-place. PlayerAim will fall back to its own turn speed and " +
                              "the feet will not step round.");

        if (jump != null) BuildJump(root, locomotion, jump);
        else Debug.LogWarning("[PlayerLocomotionBuilder] No jump clip; the character will keep " +
                              "playing locomotion while airborne.");

        // Last, so the override layers sit above every base-layer state the graph can reach.
        BuildArmLayers(controller, idle, walk, run, crouchIdle, crouchForward);

        EnableIkPass(controller);
        RemoveStrayBlendParameter(controller);

        AssetDatabase.SaveAssets();
        Debug.Log($"[PlayerLocomotionBuilder] Rebuilt {ControllerPath}", controller);
        Selection.activeObject = controller;
    }

    // Strips the controller back to a single empty layer, keeping the asset (and its GUID) alive.
    private static void ResetController(AnimatorController controller)
    {
        while (controller.parameters.Length > 0) controller.RemoveParameter(0);
        while (controller.layers.Length > 1) controller.RemoveLayer(1);

        if (controller.layers.Length == 0) controller.AddLayer("Base Layer");

        AnimatorStateMachine root = controller.layers[0].stateMachine;

        foreach (AnimatorStateTransition transition in root.anyStateTransitions.ToArray())
            root.RemoveAnyStateTransition(transition);

        foreach (ChildAnimatorState child in root.states.ToArray()) root.RemoveState(child.state);

        foreach (ChildAnimatorStateMachine child in root.stateMachines.ToArray())
            root.RemoveStateMachine(child.stateMachine);

        // Blend trees live as sub-assets and are not collected when their state goes away. The
        // same is true of a removed layer's state machine, which is why the base one is kept back.
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (asset is BlendTree tree) Object.DestroyImmediate(tree, true);
            else if (asset is AnimatorStateMachine machine && machine != root) Object.DestroyImmediate(machine, true);
            // Any state still in the file at this point belonged to a layer that has just gone.
            else if (asset is AnimatorState orphan) Object.DestroyImmediate(orphan, true);
        }
    }

    // Speed axis: 0 idle, 1 walk, 2 sprint. Each moving tier is its own 2D strafe tree, so the
    // four directional clips are authored once per tier and the parent handles the gear change.
    private static AnimatorState BuildLocomotion(AnimatorController controller, AnimationClip idle,
        StrafeSet walk, StrafeSet run)
    {
        AnimatorState state = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;

        tree.AddChild(idle, 0.0f);

        CreateStrafeTree(tree, "Walk", 1.0f, walk);

        // Real run clips when they exist; otherwise the walk clips played fast, which is the
        // stopgap this project started with.
        CreateStrafeTree(tree, "Sprint", 2.0f, run.IsComplete ? run : walk.AtSpeed(SprintClipSpeed));

        return state;
    }

    // 2D Simple Directional is the right blend type for exactly one clip per compass direction:
    // it blends the two clips either side of the requested heading instead of smearing all four.
    private static void CreateStrafeTree(BlendTree parent, string name, float threshold, StrafeSet clips)
    {
        BlendTree tree = parent.CreateBlendTreeChild(threshold);
        tree.name = name;
        tree.blendType = BlendTreeType.SimpleDirectional2D;
        tree.blendParameter = "MoveX";
        tree.blendParameterY = "MoveY";

        tree.AddChild(clips.Forward, new Vector2(0.0f, 1.0f));
        tree.AddChild(clips.Backward, new Vector2(0.0f, -1.0f));
        tree.AddChild(clips.Left, new Vector2(-1.0f, 0.0f));
        tree.AddChild(clips.Right, new Vector2(1.0f, 0.0f));

        SetChildSpeeds(tree, clips.Speed);
    }

    // Same shape as the standing tier, one gear down: idle at 0, a strafe tree at 1. The crouch
    // Speed axis is normalised against the crouch speed in PlayerAnimator, so it only runs 0..1.
    private static AnimatorState BuildCrouch(AnimatorController controller, AnimationClip idle, StrafeSet clips)
    {
        AnimatorState state = controller.CreateBlendTreeInController("Crouch", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;

        // Falling back to the forward clip for idle keeps the tree valid; freezing it at time 0
        // is what makes it read as standing still.
        tree.AddChild(idle != null ? idle : clips.Forward, 0.0f);

        if (idle == null)
        {
            ChildMotion[] children = tree.children;
            children[0].timeScale = 0.0f;
            tree.children = children;
        }

        // No crouching backward clip, so the forward one runs in reverse for that quadrant. It
        // reads acceptably at crouch pace; swap in a real clip here when there is one.
        if (clips.IsComplete)
        {
            CreateStrafeTree(tree, "CrouchWalk", 1.0f, clips);
            ReverseBackwardChild(tree);
        }
        else
        {
            Debug.LogWarning("[PlayerLocomotionBuilder] Incomplete crouch strafe set; crouching " +
                             "uses the forward clip in every direction.");
            CreateStrafeTree(tree, "CrouchWalk", 1.0f,
                new StrafeSet(clips.Forward, clips.Forward, clips.Forward, clips.Forward));
        }

        return state;
    }

    // The strafe tree is the last child added, and its backward clip is the second child in it.
    private static void ReverseBackwardChild(BlendTree parent)
    {
        ChildMotion[] tiers = parent.children;
        if (tiers.Length == 0 || !(tiers[tiers.Length - 1].motion is BlendTree strafe)) return;

        ChildMotion[] children = strafe.children;
        if (children.Length < 2) return;

        children[1].timeScale = -1.0f;
        strafe.children = children;
    }

    // The strafe clips were authored with the character holding a torch, so their arms are wrong
    // for this game, crouched and standing alike. These layers replay the forward clips' arms over
    // the top of whatever the base layer is blending, through a mask that lets nothing but the arms
    // and fingers through. The legs, hips and spine keep coming from the base layer, so a strafe
    // still reads as a strafe.
    //
    // One layer per stance, because their clips differ. PlayerAnimator owns both weights and keeps
    // them complementary, so the arms only ever come from the stance the character is in.
    private static void BuildArmLayers(AnimatorController controller, AnimationClip standIdle,
        StrafeSet walk, StrafeSet run, AnimationClip crouchIdle, AnimationClip crouchForward)
    {
        // Same Speed axis as the legs below them: a stop settles the arms, a walk swings them, and
        // a sprint swings them harder.
        BuildArmLayer(controller, StandingArmsLayer, new (Motion, float)[]
        {
            (standIdle, 0.0f),
            (walk.Forward, 1.0f),
            (run.IsComplete ? run.Forward : walk.Forward, 2.0f)
        });

        if (crouchForward == null) return;

        // A crouching stop takes the standing idle arms outright: the crouch idle clip carries the
        // torch pose too, and nothing about holding still is stance-specific from the shoulders up.
        BuildArmLayer(controller, CrouchArmsLayer, new (Motion, float)[]
        {
            (standIdle != null ? standIdle : crouchForward, 0.0f),
            (crouchForward, 1.0f)
        });
    }

    private static void BuildArmLayer(AnimatorController controller, string name,
        (Motion Motion, float Threshold)[] children)
    {
        AnimatorControllerLayer layer = new AnimatorControllerLayer
        {
            name = name,
            defaultWeight = 0.0f,
            avatarMask = BuildArmMask(),
            blendingMode = AnimatorLayerBlendingMode.Override,
            stateMachine = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy }
        };

        // The state machine is a sub-asset of the controller, like the ones AddLayer creates.
        AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);

        // Built by hand rather than with CreateBlendTreeInController, which always builds into
        // layer 0 and would have to be moved off it afterwards.
        BlendTree tree = new BlendTree
        {
            name = name,
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            useAutomaticThresholds = false,
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(tree, controller);

        foreach ((Motion motion, float threshold) in children)
            if (motion != null) tree.AddChild(motion, threshold);

        AnimatorState state = layer.stateMachine.AddState(name, new Vector3(300.0f, 60.0f, 0.0f));
        state.motion = tree;
        state.writeDefaultValues = false;
        layer.stateMachine.defaultState = state;

        controller.AddLayer(layer);
    }

    // Arms and fingers only. Shoulders belong to the arm body parts in a humanoid mask, which is
    // what carries the torch pose, and leaving the body out keeps PlayerAimTwist in charge of the
    // spine.
    private static AvatarMask BuildArmMask() => BuildMask(ArmMaskPath,
        AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
        AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers);

    private static AvatarMask BuildMask(string path, params AvatarMaskBodyPart[] active)
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, path);
        }

        foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
        {
            if (part == AvatarMaskBodyPart.LastBodyPart) continue;
            mask.SetHumanoidBodyPartActive(part, System.Array.IndexOf(active, part) >= 0);
        }

        EditorUtility.SetDirty(mask);
        return mask;
    }

    // Dedicated transition states rather than a plain crossfade: the standing/crouching poses are
    // far enough apart that a blend reads as the character melting between them.
    private static void WireCrouchTransitions(AnimatorStateMachine root, AnimatorState locomotion,
        AnimatorState crouch, AnimationClip toCrouch, AnimationClip toStanding)
    {
        if (toCrouch == null || toStanding == null)
        {
            Debug.LogWarning("[PlayerLocomotionBuilder] Missing a crouch transition clip; crossfading instead.");
            Connect(locomotion, crouch, "Crouching", true, 0.15f);
            Connect(crouch, locomotion, "Crouching", false, 0.15f);
            return;
        }

        AnimatorState standToCrouch = root.AddState("StandToCrouch", new Vector3(560.0f, 60.0f, 0.0f));
        standToCrouch.motion = toCrouch;
        standToCrouch.writeDefaultValues = false;

        AnimatorState crouchToStand = root.AddState("CrouchToStand", new Vector3(560.0f, 240.0f, 0.0f));
        crouchToStand.motion = toStanding;
        crouchToStand.writeDefaultValues = false;

        Connect(locomotion, standToCrouch, "Crouching", true, 0.1f);
        Connect(crouch, crouchToStand, "Crouching", false, 0.1f);

        // Let the opposite input interrupt a transition, so tapping crouch twice quickly does
        // not lock the player into finishing a pose change they already cancelled.
        Connect(standToCrouch, crouchToStand, "Crouching", false, 0.1f);
        Connect(crouchToStand, standToCrouch, "Crouching", true, 0.1f);

        Advance(standToCrouch, crouch);
        Advance(crouchToStand, locomotion);
    }

    // Turn-in-place: PlayerAim decides when the cursor has led the body too far, and these states
    // supply the footwork for it. The clips carry their rotation as root motion, which the player
    // discards, so the feet step while PlayerAim turns the transform by exactly the clip's angle.
    // Without that split the turn would be applied twice.
    private static void BuildTurns(AnimatorStateMachine root, AnimatorState locomotion,
        AnimationClip left, AnimationClip right)
    {
        AnimatorState turnLeft = AddTurnState(root, "TurnLeft", left, new Vector3(300.0f, -120.0f, 0.0f));
        AnimatorState turnRight = AddTurnState(root, "TurnRight", right, new Vector3(560.0f, -120.0f, 0.0f));

        EnterTurn(locomotion, turnLeft, AnimatorConditionMode.Less, -0.5f);
        EnterTurn(locomotion, turnRight, AnimatorConditionMode.Greater, 0.5f);

        // Out the moment the turn finishes, so a short correction does not hold the whole clip.
        ExitTurn(turnLeft, locomotion, AnimatorConditionMode.Greater, -0.5f);
        ExitTurn(turnRight, locomotion, AnimatorConditionMode.Less, 0.5f);

        // Walking off mid-turn hands the feet back to the blend tree.
        ExitTurn(turnLeft, locomotion, AnimatorConditionMode.Greater, StandingSpeed, "Speed");
        ExitTurn(turnRight, locomotion, AnimatorConditionMode.Greater, StandingSpeed, "Speed");

        // A turn wider than one clip runs out of footage. Falling back to Locomotion re-arms the
        // entry transition, so the character takes a second step instead of freezing mid-pose.
        Advance(turnLeft, locomotion);
        Advance(turnRight, locomotion);
    }

    private static AnimatorState AddTurnState(AnimatorStateMachine root, string name,
        AnimationClip clip, Vector3 position)
    {
        AnimatorState state = root.AddState(name, position);
        state.motion = clip;
        state.tag = TurnTag;
        state.writeDefaultValues = false;
        return state;
    }

    private static void EnterTurn(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.AddCondition(mode, threshold, "Turn");
        transition.AddCondition(AnimatorConditionMode.Less, StandingSpeed, "Speed");
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.1f;
        transition.canTransitionToSelf = false;
    }

    private static void ExitTurn(AnimatorState from, AnimatorState to, AnimatorConditionMode mode,
        float threshold, string parameter = "Turn")
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.AddCondition(mode, threshold, parameter);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.12f;
        transition.canTransitionToSelf = false;
    }

    // One jump clip for both cases. The clip is a running jump, so a standing jump enters it over
    // a long blend: the idle pose is held well into the takeoff and the clip's forward lean and
    // arm swing arrive late and weak. A running jump gets the short blend, where that lean is
    // wanted. Hiding the run in the blend length is what a second clip would otherwise pay for.
    //
    // Jumps hang off Any State: the character can leave the ground out of locomotion, out of a
    // turn or mid-crouch-transition, and one trigger covers all of them without a transition per
    // source state.
    private static void BuildJump(AnimatorStateMachine root, AnimatorState locomotion, AnimationClip clip)
    {
        AnimatorState jump = root.AddState("Jump", new Vector3(560.0f, -260.0f, 0.0f));
        jump.motion = clip;
        jump.writeDefaultValues = false;

        EnterJump(root, jump, AnimatorConditionMode.Greater, RunningJumpSpeed, RunningJumpBlend);
        EnterJump(root, jump, AnimatorConditionMode.Less, RunningJumpSpeed, StandingJumpBlend);

        ExitJump(jump, locomotion);
    }

    private static void EnterJump(AnimatorStateMachine root, AnimatorState to,
        AnimatorConditionMode mode, float threshold, float duration)
    {
        AnimatorStateTransition transition = root.AddAnyStateTransition(to);
        transition.AddCondition(AnimatorConditionMode.If, 0.0f, "Jump");
        transition.AddCondition(mode, threshold, "Speed");
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
    }

    private static void ExitJump(AnimatorState from, AnimatorState to)
    {
        // Grounded ends the jump, but not before the clip has left the floor: the controller still
        // reports grounded on the frame the jump launches, and an unguarded condition would bounce
        // the state straight back into Locomotion.
        AnimatorStateTransition landing = from.AddTransition(to);
        landing.AddCondition(AnimatorConditionMode.If, 0.0f, "Grounded");
        landing.hasExitTime = true;
        landing.exitTime = 0.25f;
        landing.hasFixedDuration = true;
        // Long enough that the clip's run-out does not snap into idle on the landing frame.
        landing.duration = 0.2f;

        // There is no fall clip, so a jump that outlasts its own footage hands back to locomotion
        // rather than holding the landing pose in mid-air. Add a fall state here when that clip
        // exists; it is the right place for it.
        Advance(from, to);
    }

    private static void Connect(AnimatorState from, AnimatorState to, string parameter, bool value, float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0.0f, parameter);
        // No exit time: the graph reacts on the frame the input changes, which is what made the
        // old bool-driven graph feel laggy.
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
    }

    // Plays the pose change through, then falls into the steady state.
    private static void Advance(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = 0.85f;
        transition.hasFixedDuration = true;
        transition.duration = 0.15f;
    }

    // Creating a blend tree in code auto-adds a float called "Blend" as its default axis. Every
    // tree here is repointed at MoveX/MoveY/Speed, so that parameter is left over.
    private static void RemoveStrayBlendParameter(AnimatorController controller)
    {
        for (int i = controller.parameters.Length - 1; i >= 0; i--)
            if (controller.parameters[i].name == "Blend") controller.RemoveParameter(i);
    }

    private static void SetChildSpeeds(BlendTree tree, float speed)
    {
        ChildMotion[] children = tree.children;
        for (int i = 0; i < children.Length; i++) children[i].timeScale = speed;
        tree.children = children;
    }

    // layers returns a copy, so the whole array has to be written back for iKPass to stick.
    private static void EnableIkPass(AnimatorController controller)
    {
        AnimatorControllerLayer[] layers = controller.layers;
        layers[0].name = "Base Layer";
        layers[0].iKPass = true;
        layers[0].defaultWeight = 1.0f;
        // Full-body locomotion: a mask here would only subtract bones. Masks belong on an
        // additive upper-body layer, which is where an aim or weapon pose would go later.
        layers[0].avatarMask = null;
        controller.layers = layers;
    }

    private static void LayoutStates(AnimatorStateMachine root, AnimatorState locomotion, AnimatorState crouch)
    {
        ChildAnimatorState[] states = root.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state == locomotion) states[i].position = new Vector3(300.0f, 60.0f, 0.0f);
            else if (crouch != null && states[i].state == crouch) states[i].position = new Vector3(300.0f, 240.0f, 0.0f);
        }
        root.states = states;

        root.entryPosition = new Vector3(60.0f, 60.0f, 0.0f);
        root.anyStatePosition = new Vector3(60.0f, 300.0f, 0.0f);
        root.exitPosition = new Vector3(820.0f, 300.0f, 0.0f);
    }

    private static AnimationClip LoadClip(string relativePath)
    {
        string path = $"{AnimationsRoot}/{relativePath}";

        // An imported FBX holds its clip as a sub-asset, alongside a hidden __preview__ copy.
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        if (clip == null) Debug.LogWarning($"[PlayerLocomotionBuilder] No clip in {path}");
        return clip;
    }
}
