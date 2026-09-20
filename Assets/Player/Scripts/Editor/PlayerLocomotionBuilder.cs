using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Rebuilds the player locomotion graph from scratch via Animator > Rebuild Player Locomotion.
// Authoring it in code rather than by hand keeps the blend thresholds, damping and transition
// settings reviewable in git, and makes a bad experiment one menu click away from being undone.
public static class PlayerLocomotionBuilder
{
    private const string ControllerPath = "Assets/Player/PlayerAnimationController.controller";
    private const string AnimationsRoot = "Assets/Player/Animations";

    // Playback multiplier on the sprint tree until real sprint clips exist: reusing the walk
    // clips faster is the standard stopgap, and it keeps the graph shape final.
    private const float SprintClipSpeed = 1.45f;

    [MenuItem("Animator/Rebuild Player Locomotion")]
    public static void Rebuild()
    {
        AnimationClip idle = LoadClip("Idle/Idle.fbx");
        AnimationClip walkForward = LoadClip("Walking/walking_forward.fbx");
        AnimationClip walkBackward = LoadClip("Walking/walking_backward.fbx");
        AnimationClip walkLeft = LoadClip("Walking/walking_left.fbx");
        AnimationClip walkRight = LoadClip("Walking/walking_right.fbx");
        AnimationClip crouchForward = LoadClip("Crouched/crouch_walking_forward.fbx");
        AnimationClip toCrouch = LoadClip("Standing/standing_trans_crouch.fbx");
        AnimationClip toStanding = LoadClip("Crouched/crouch_trans_standing.fbx");

        if (idle == null || walkForward == null || walkBackward == null || walkLeft == null || walkRight == null)
        {
            Debug.LogError("[PlayerLocomotionBuilder] Missing a required locomotion clip. Aborting.");
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

        AnimatorStateMachine root = controller.layers[0].stateMachine;

        AnimatorState locomotion = BuildLocomotion(controller, idle, walkForward, walkBackward, walkLeft, walkRight);
        locomotion.writeDefaultValues = false;
        root.defaultState = locomotion;

        // No crouch clip means no crouch state. An empty state would still be entered when
        // Crouching goes true and would freeze the rig mid-pose, which reads as a broken
        // character; staying in Locomotion is merely the wrong animation.
        if (crouchForward != null)
        {
            AnimatorState crouch = BuildCrouch(controller, crouchForward);
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

        // Blend trees live as sub-assets and are not collected when their state goes away.
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            if (asset is BlendTree tree) Object.DestroyImmediate(tree, true);
    }

    // Speed axis: 0 idle, 1 walk, 2 sprint. Each moving tier is its own 2D strafe tree, so the
    // four directional clips are authored once per tier and the parent handles the gear change.
    private static AnimatorState BuildLocomotion(AnimatorController controller, AnimationClip idle,
        AnimationClip forward, AnimationClip backward, AnimationClip left, AnimationClip right)
    {
        AnimatorState state = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;

        tree.AddChild(idle, 0.0f);

        CreateStrafeTree(tree, "Walk", 1.0f, forward, backward, left, right, 1.0f);
        CreateStrafeTree(tree, "Sprint", 2.0f, forward, backward, left, right, SprintClipSpeed);

        return state;
    }

    // 2D Simple Directional is the right blend type for exactly one clip per compass direction:
    // it blends the two clips either side of the requested heading instead of smearing all four.
    private static void CreateStrafeTree(BlendTree parent, string name, float threshold,
        AnimationClip forward, AnimationClip backward, AnimationClip left, AnimationClip right, float clipSpeed)
    {
        BlendTree tree = parent.CreateBlendTreeChild(threshold);
        tree.name = name;
        tree.blendType = BlendTreeType.SimpleDirectional2D;
        tree.blendParameter = "MoveX";
        tree.blendParameterY = "MoveY";

        tree.AddChild(forward, new Vector2(0.0f, 1.0f));
        tree.AddChild(backward, new Vector2(0.0f, -1.0f));
        tree.AddChild(left, new Vector2(-1.0f, 0.0f));
        tree.AddChild(right, new Vector2(1.0f, 0.0f));

        SetChildSpeeds(tree, clipSpeed);
    }

    private static AnimatorState BuildCrouch(AnimatorController controller, AnimationClip crouchForward)
    {
        AnimatorState state = controller.CreateBlendTreeInController("Crouch", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;

        // Only a forward crouch clip exists, so the same clip sits at both ends of the axis and
        // is frozen at idle. Drop crouch_idle and crouch strafe clips in and extend this into a
        // CreateStrafeTree call to match the standing tier.
        tree.AddChild(crouchForward, 0.0f);
        tree.AddChild(crouchForward, 1.0f);

        ChildMotion[] children = tree.children;
        children[0].timeScale = 0.0f;
        tree.children = children;

        return state;
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
