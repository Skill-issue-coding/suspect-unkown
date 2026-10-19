using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Rebuilds the player locomotion graph from scratch via Animator > Rebuild Player Locomotion.
// Authoring it in code rather than by hand keeps the blend thresholds, damping and transition
// settings reviewable in git, and makes a bad experiment one menu click away from being undone.
//
// Every clip but the jump comes from Player.fbx. The graph has two stances, each with an idle, a walking
// tier (and a sprint tier standing) and turn-in-place; and one idle break per stance, a random variant
// that PlayerAnimator fires after the player has stood still for a while.
public static class PlayerLocomotionBuilder
{
    // One clip per compass direction. A tier of the Speed axis is exactly this, so passing it around
    // as a set keeps the eight-argument version of BuildLocomotion away.
    private readonly struct StrafeSet
    {
        public readonly AnimationClip Forward;
        public readonly AnimationClip Backward;
        public readonly AnimationClip Left;
        public readonly AnimationClip Right;

        public StrafeSet(AnimationClip forward, AnimationClip backward, AnimationClip left, AnimationClip right)
        {
            Forward = forward;
            Backward = backward;
            Left = left;
            Right = right;
        }

        // A tier with a hole in it would blend toward an empty motion, freezing the character
        // whenever it faced that way, so a partial set is treated as no set at all.
        public bool IsComplete => Forward != null && Backward != null && Left != null && Right != null;
    }

    private const string ControllerPath = "Assets/Player/Art/PlayerAnimationController.controller";

    // Player.fbx has no jump. This is the one Mixamo clip left; replace it here when a real one exists.
    private const string JumpClipPath = "Assets/Player/Art/Animations/Running/running_jump.fbx";

    // PlayerAim reads the length of whichever state carries this tag, and paces the body's
    // rotation to it. Renaming the tag here means renaming TurnTag there.
    private const string TurnTag = "Turn";

    // PlayerAnimator writes these. The variant is a whole number from 1 to the number of clips in the
    // stance's idle break, picked before the trigger fires. Renaming either means renaming it there.
    private const string IdleBreakTrigger = "IdleBreak";
    private const string IdleVariantParameter = "IdleVariant";

    // Below this Speed the character counts as standing still, so turn-in-place and idle breaks are allowed.
    private const float StandingSpeed = 0.1f;

    // Speed above which a jump is treated as a running one. Half a step into the walk tier:
    // anything with real forward motion reads wrong as a standing jump.
    private const float RunningJumpSpeed = 0.5f;

    // Blend lengths into the single jump clip. The running jump snaps in, because the character
    // is already leaning. The standing jump eases in over a third of a second, which keeps the
    // idle pose through the crouch and takeoff and takes most of the run out of the clip.
    private const float RunningJumpBlend = 0.08f;
    private const float StandingJumpBlend = 0.32f;

    // Standing and crouching are far apart in pose but have no transition clips, so the change is a
    // crossfade this long
    private const float StanceBlend = 0.2f;

    [MenuItem(MenuPaths.PlayerRebuildLocomotion, false, MenuPaths.PlayerPriority)]
    public static void Rebuild()
    {
        // The loop and bake flags are part of what the graph needs, so they are set before the clips are read
        PlayerClipImport.Apply();

        // The idles and the locomotion cycles use the reworked "_New" clips (crouch 1, walk 2, run 4 m/s).
        // The idle breaks and turns have no reworked version yet and keep their original clips.
        AnimationClip standIdle = LoadClip("Player_Idle_Stand_New");
        AnimationClip crouchIdle = LoadClip("Player_Idle_Crouch_New");
        AnimationClip[] standBreaks = { LoadClip("Player_Idle_Stand_Impatient"), LoadClip("Player_Idle_Stand_Phone") };
        AnimationClip[] crouchBreaks = { LoadClip("Player_Idle_Crouch_Peek"), LoadClip("Player_Idle_Crouch_Scheme") };

        StrafeSet walk = new StrafeSet(LoadClip("Player_Walk_Forward_New"), LoadClip("Player_Walk_Backward_New"),
            LoadClip("Player_Walk_Left_New"), LoadClip("Player_Walk_Right_New"));
        StrafeSet run = new StrafeSet(LoadClip("Player_Run_Forward_New"), LoadClip("Player_Run_Backward_New"),
            LoadClip("Player_Run_Left_New"), LoadClip("Player_Run_Right_New"));
        StrafeSet crouch = new StrafeSet(LoadClip("Player_Crouch_Forward_New"), LoadClip("Player_Crouch_Backward_New"),
            LoadClip("Player_Crouch_Left_New"), LoadClip("Player_Crouch_Right_New"));

        AnimationClip turnLeft = LoadClip("Player_Turn_Left_Stand");
        AnimationClip turnRight = LoadClip("Player_Turn_Right_Stand");
        AnimationClip crouchTurnLeft = LoadClip("Player_Turn_Left_Crouch");
        AnimationClip crouchTurnRight = LoadClip("Player_Turn_Right_Crouch");
        AnimationClip jump = LoadJumpClip();

        // Anything missing aborts: a state with no clip would hold the rig frozen mid-pose
        bool complete = standIdle != null && crouchIdle != null && walk.IsComplete && run.IsComplete
                        && crouch.IsComplete && turnLeft != null && turnRight != null
                        && crouchTurnLeft != null && crouchTurnRight != null
                        && standBreaks.All(c => c != null) && crouchBreaks.All(c => c != null);
        if (!complete)
        {
            Debug.LogError("[PlayerLocomotionBuilder] Missing a clip from Player.fbx. Aborting.");
            return;
        }

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
        controller.AddParameter(IdleBreakTrigger, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(IdleVariantParameter, AnimatorControllerParameterType.Float);

        AnimatorStateMachine root = controller.layers[0].stateMachine;

        AnimatorState standing = BuildLocomotion(controller, standIdle, walk, run);
        standing.writeDefaultValues = false;
        root.defaultState = standing;

        AnimatorState crouching = BuildCrouch(controller, crouchIdle, crouch);
        crouching.writeDefaultValues = false;

        AnimatorState standBreak = BuildIdleBreak(controller, "IdleBreakStanding", standBreaks);
        AnimatorState crouchBreak = BuildIdleBreak(controller, "IdleBreakCrouching", crouchBreaks);

        Connect(standing, crouching, "Crouching", true, StanceBlend);
        Connect(crouching, standing, "Crouching", false, StanceBlend);

        WireIdleBreak(standing, standBreak, crouching, otherStanceCrouching: true);
        WireIdleBreak(crouching, crouchBreak, standing, otherStanceCrouching: false);

        BuildTurns(root, standing, turnLeft, turnRight, "Turn", new Vector3(300.0f, -120.0f, 0.0f));
        BuildTurns(root, crouching, crouchTurnLeft, crouchTurnRight, "CrouchTurn", new Vector3(300.0f, 420.0f, 0.0f));

        if (jump != null) BuildJump(root, standing, jump);
        else Debug.LogWarning("[PlayerLocomotionBuilder] No jump clip; the character will keep " +
                              "playing locomotion while airborne.");

        EnableIkPass(controller);
        RemoveStrayBlendParameter(controller);
        LayoutStates(root, standing, crouching, standBreak, crouchBreak);

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
        CreateStrafeTree(tree, "Sprint", 2.0f, run);

        return state;
    }

    // Same shape as the standing tier, one gear down: idle at 0, a strafe tree at 1. The crouch
    // Speed axis is normalised against the crouch speed in PlayerAnimator, so it only runs 0..1.
    private static AnimatorState BuildCrouch(AnimatorController controller, AnimationClip idle, StrafeSet clips)
    {
        AnimatorState state = controller.CreateBlendTreeInController("Crouch", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;

        tree.AddChild(idle, 0.0f);
        CreateStrafeTree(tree, "CrouchWalk", 1.0f, clips);

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
    }

    // One state per stance that plays one of its variants. A 1D tree on the variant number holds each
    // clip at weight 1 exactly at its whole number, so PlayerAnimator picks a variant by writing 1, 2, ...
    // and never sees a blend between two idles. The clips do not loop: the state hands back when one ends.
    private static AnimatorState BuildIdleBreak(AnimatorController controller, string name, AnimationClip[] variants)
    {
        AnimatorState state = controller.CreateBlendTreeInController(name, out BlendTree tree, 0);
        state.writeDefaultValues = false;
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = IdleVariantParameter;
        tree.useAutomaticThresholds = false;

        for (int i = 0; i < variants.Length; i++) tree.AddChild(variants[i], i + 1.0f);

        return state;
    }

    // Standing still long enough fires the trigger. The break ends when its clip does, and at once on
    // anything the player does: moving, turning round or changing stance. A turn goes back through the
    // base state first, which re-enters its own turn state on the next frame.
    private static void WireIdleBreak(AnimatorState baseState, AnimatorState breakState,
        AnimatorState otherStance, bool otherStanceCrouching)
    {
        AnimatorStateTransition enter = baseState.AddTransition(breakState);
        enter.AddCondition(AnimatorConditionMode.If, 0.0f, IdleBreakTrigger);
        enter.AddCondition(AnimatorConditionMode.Less, StandingSpeed, "Speed");
        enter.hasExitTime = false;
        enter.hasFixedDuration = true;
        enter.duration = 0.25f;
        enter.canTransitionToSelf = false;

        ExitBreak(breakState, baseState, AnimatorConditionMode.Greater, StandingSpeed, "Speed");
        ExitBreak(breakState, baseState, AnimatorConditionMode.Less, -0.5f, "Turn");
        ExitBreak(breakState, baseState, AnimatorConditionMode.Greater, 0.5f, "Turn");

        AnimatorStateTransition swap = breakState.AddTransition(otherStance);
        swap.AddCondition(otherStanceCrouching ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0.0f, "Crouching");
        swap.hasExitTime = false;
        swap.hasFixedDuration = true;
        swap.duration = StanceBlend;
        swap.canTransitionToSelf = false;

        // Playing through: nearly to the end, so the clip's last frame is never held
        AnimatorStateTransition finish = breakState.AddTransition(baseState);
        finish.hasExitTime = true;
        finish.exitTime = 0.95f;
        finish.hasFixedDuration = true;
        finish.duration = 0.25f;
    }

    private static void ExitBreak(AnimatorState from, AnimatorState to, AnimatorConditionMode mode,
        float threshold, string parameter)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.AddCondition(mode, threshold, parameter);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.2f;
        transition.canTransitionToSelf = false;
    }

    // Turn-in-place: PlayerAim decides when the cursor has led the body too far, and these states
    // supply the footwork for it. The clips carry their rotation as root motion, which the player
    // discards, so the feet step while PlayerAim turns the transform by exactly the clip's angle.
    // Without that split the turn would be applied twice. One pair per stance.
    private static void BuildTurns(AnimatorStateMachine root, AnimatorState baseState,
        AnimationClip left, AnimationClip right, string namePrefix, Vector3 position)
    {
        AnimatorState turnLeft = AddTurnState(root, namePrefix + "Left", left, position);
        AnimatorState turnRight = AddTurnState(root, namePrefix + "Right", right, position + new Vector3(260.0f, 0.0f, 0.0f));

        EnterTurn(baseState, turnLeft, AnimatorConditionMode.Less, -0.5f);
        EnterTurn(baseState, turnRight, AnimatorConditionMode.Greater, 0.5f);

        // Out the moment the turn finishes, so a short correction does not hold the whole clip.
        ExitTurn(turnLeft, baseState, AnimatorConditionMode.Greater, -0.5f);
        ExitTurn(turnRight, baseState, AnimatorConditionMode.Less, 0.5f);

        // Walking off mid-turn hands the feet back to the blend tree.
        ExitTurn(turnLeft, baseState, AnimatorConditionMode.Greater, StandingSpeed, "Speed");
        ExitTurn(turnRight, baseState, AnimatorConditionMode.Greater, StandingSpeed, "Speed");

        // A turn wider than one clip runs out of footage. Falling back to the base state re-arms the
        // entry transition, so the character takes a second step instead of freezing mid-pose.
        Advance(turnLeft, baseState);
        Advance(turnRight, baseState);
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
    // turn or an idle break, and one trigger covers all of them without a transition per
    // source state.
    private static void BuildJump(AnimatorStateMachine root, AnimatorState standing, AnimationClip clip)
    {
        AnimatorState jump = root.AddState("Jump", new Vector3(560.0f, -260.0f, 0.0f));
        jump.motion = clip;
        jump.writeDefaultValues = false;

        EnterJump(root, jump, AnimatorConditionMode.Greater, RunningJumpSpeed, RunningJumpBlend);
        EnterJump(root, jump, AnimatorConditionMode.Less, RunningJumpSpeed, StandingJumpBlend);

        ExitJump(jump, standing);
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

    // Plays the clip through, then falls into the steady state.
    private static void Advance(AnimatorState from, AnimatorState to)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = 0.85f;
        transition.hasFixedDuration = true;
        transition.duration = 0.15f;
    }

    // Creating a blend tree in code auto-adds a float called "Blend" as its default axis. Every
    // tree here is repointed at MoveX/MoveY/Speed/IdleVariant, so that parameter is left over.
    private static void RemoveStrayBlendParameter(AnimatorController controller)
    {
        for (int i = controller.parameters.Length - 1; i >= 0; i--)
            if (controller.parameters[i].name == "Blend") controller.RemoveParameter(i);
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

    private static void LayoutStates(AnimatorStateMachine root, AnimatorState standing, AnimatorState crouching,
        AnimatorState standBreak, AnimatorState crouchBreak)
    {
        ChildAnimatorState[] states = root.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state == standing) states[i].position = new Vector3(300.0f, 60.0f, 0.0f);
            else if (states[i].state == crouching) states[i].position = new Vector3(300.0f, 240.0f, 0.0f);
            else if (states[i].state == standBreak) states[i].position = new Vector3(560.0f, 60.0f, 0.0f);
            else if (states[i].state == crouchBreak) states[i].position = new Vector3(560.0f, 240.0f, 0.0f);
        }
        root.states = states;

        root.entryPosition = new Vector3(60.0f, 60.0f, 0.0f);
        root.anyStatePosition = new Vector3(60.0f, 300.0f, 0.0f);
        root.exitPosition = new Vector3(820.0f, 300.0f, 0.0f);
    }

    // A clip of Player.fbx by its short name. The FBX holds each clip as a sub-asset, prefixed with
    // its armature's name, alongside a hidden __preview__ copy. The Cartoon clips are never asked for.
    private static AnimationClip LoadClip(string shortName)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(PlayerClipImport.FbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__") && c.name.EndsWith("|" + shortName));

        if (clip == null) Debug.LogWarning($"[PlayerLocomotionBuilder] No clip {shortName} in {PlayerClipImport.FbxPath}");
        return clip;
    }

    private static AnimationClip LoadJumpClip()
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(JumpClipPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        if (clip == null) Debug.LogWarning($"[PlayerLocomotionBuilder] No clip in {JumpClipPath}");
        return clip;
    }
}
