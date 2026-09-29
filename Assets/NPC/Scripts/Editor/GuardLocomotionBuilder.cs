using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Rebuilds the guard locomotion graph from scratch via Tools > Suspect Unknown > NPC > Guard > Rebuild Locomotion.
// Guards never strafe: GuardBehaviour turns the body toward where it is heading, so one 1D tree on
// Speed (idle, walk, run) is the whole graph. Thresholds are in metres per second, measured from the
// clips themselves, so NpcAnimator feeds the agent's speed straight in and the feet match the ground.
public static class GuardLocomotionBuilder
{
    // Fallback speeds for clips exported "In Place", which carry no root motion to measure.
    // GuardBehaviour's patrolSpeed, investigateSpeed and pursueSpeed must match the thresholds the
    // rebuild logs, or the feet slide.
    private const float FallbackPatrolSpeed = 1.5f;
    private const float FallbackInvestigateSpeed = 2.5f;
    private const float FallbackRunSpeed = 4.5f;

    private const string ControllerPath = "Assets/NPC/Animators/GuardAnimator.controller";
    private const string ClipsRoot = "Assets/NPC/Art/Guard/Animation Clips";

    // Below this, a measured speed is treated as "no root motion" rather than a very slow clip
    private const float MinMeasurableSpeed = 0.05f;

    [MenuItem(MenuPaths.GuardRebuildLocomotion, false, MenuPaths.NpcPriority)]
    public static void Rebuild()
    {
        AnimationClip idle = LoadClip("Patrolling/guard_patrolling_idle_1.fbx");
        AnimationClip patrolWalk = LoadClip("Patrolling/guard_patrol_walking.fbx");
        AnimationClip investigateWalk = LoadClip("Investigating/guard_investigating_walk.fbx");
        AnimationClip search = LoadClip("Investigating/guard_investigating_looking.fbx");
        AnimationClip run = LoadClip("Pursuing/guard_pursuing_run.fbx");
        AnimationClip watch = LoadClip("Watching/guard_watching.fbx");
        AnimationClip giveUp = LoadClip("Transitions/guard_giving_up_1.fbx");

        if (idle == null || patrolWalk == null)
        {
            Debug.LogError("[NpcLocomotionBuilder] Missing the patrol idle or walk. Aborting.");
            return;
        }

        EnsureFolder(Path.GetDirectoryName(ControllerPath));

        // Reset in place rather than delete and recreate: a new asset gets a new GUID and would
        // silently unhook the Animator on every guard prefab
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        else ResetController(controller);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Watching", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Searching", AnimatorControllerParameterType.Bool);
        controller.AddParameter("GiveUp", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorState locomotion = BuildLocomotion(controller, idle, patrolWalk, investigateWalk, run);
        root.defaultState = locomotion;

        if (watch != null) AddToggleState(root, locomotion, watch, "Watching", fromAnyState: true);
        if (search != null) AddToggleState(root, locomotion, search, "Searching", fromAnyState: false);
        if (giveUp != null) AddOneShot(root, locomotion, giveUp, "GiveUp");

        AssetDatabase.SaveAssets();
        Debug.Log($"[NpcLocomotionBuilder] Rebuilt {ControllerPath}", controller);
        Selection.activeObject = controller;
    }

    private static AnimatorState BuildLocomotion(AnimatorController controller, AnimationClip idle,
    AnimationClip patrolWalk, AnimationClip investigateWalk, AnimationClip run)
    {
        AnimatorState state = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        state.writeDefaultValues = false;
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;

        tree.AddChild(idle, 0f);
        tree.AddChild(patrolWalk, MeasureSpeed(patrolWalk, FallbackPatrolSpeed));
        if (investigateWalk != null) tree.AddChild(investigateWalk, MeasureSpeed(investigateWalk, FallbackInvestigateSpeed));
        if (run != null) tree.AddChild(run, MeasureSpeed(run, FallbackRunSpeed));

        Debug.Log("[NpcLocomotionBuilder] Thresholds: " +
                  string.Join(", ", tree.children.Select(c => $"{c.motion.name} {c.threshold:0.00} m/s")));
        return state;
    }

    // A looping state entered while a bool is true and left when it turns false
    private static void AddToggleState(AnimatorStateMachine root, AnimatorState locomotion, AnimationClip clip,
        string parameter, bool fromAnyState)
    {
        AnimatorState state = root.AddState(clip.name);
        state.motion = clip;
        state.writeDefaultValues = false;

        AnimatorStateTransition enter = fromAnyState ? root.AddAnyStateTransition(state) : locomotion.AddTransition(state);
        enter.AddCondition(AnimatorConditionMode.If, 0f, parameter);
        enter.duration = 0.2f;
        enter.hasExitTime = false;
        enter.canTransitionToSelf = false;

        AnimatorStateTransition exit = state.AddTransition(locomotion);
        exit.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter);
        exit.duration = 0.2f;
        exit.hasExitTime = false;
    }

    // A one-shot entered on a trigger that plays nearly to the end, then blends back
    private static void AddOneShot(AnimatorStateMachine root, AnimatorState locomotion, AnimationClip clip, string trigger)
    {
        AnimatorState state = root.AddState(clip.name);
        state.motion = clip;
        state.writeDefaultValues = false;

        AnimatorStateTransition enter = root.AddAnyStateTransition(state);
        enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        enter.duration = 0.2f;
        enter.hasExitTime = false;
        enter.canTransitionToSelf = false;

        AnimatorStateTransition exit = state.AddTransition(locomotion);
        exit.hasExitTime = true;
        exit.exitTime = 0.9f;
        exit.duration = 0.25f;
    }

    // averageSpeed is the clip's root motion in metres per second. A Humanoid clip exported with
    // "In Place" unchecked has it; an in-place clip reads zero and gets the fallback
    private static float MeasureSpeed(AnimationClip clip, float fallback)
    {
        float speed = new Vector3(clip.averageSpeed.x, 0.0f, clip.averageSpeed.z).magnitude;
        if (speed >= MinMeasurableSpeed) return speed;

        Debug.LogWarning($"[NpcLocomotionBuilder] {clip.name} has no root motion; using {fallback} m/s. " +
                        "Expect foot sliding until it matches the clip.");

        return fallback;
    }

    private static AnimationClip LoadClip(string fileName)
    {
        string path = $"{ClipsRoot}/{fileName}";

        // An imported FBX holds its clip as a sub-asset, alongside a hidden __preview__ copy.
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        if (clip == null) Debug.LogWarning($"[NpcLocomotionBuilder] No clip at {path}");
        return clip;
    }

    private static void EnsureFolder(string folder)
    {
        folder = folder.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
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

        foreach (ChildAnimatorState child in root.states.ToArray())
            root.RemoveState(child.state);

        foreach (ChildAnimatorStateMachine child in root.stateMachines.ToArray())
            root.RemoveStateMachine(child.stateMachine);

        // Blend trees are sub-assets and are not collected when their state goes away.
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (asset is BlendTree tree) Object.DestroyImmediate(tree, true);
            else if (asset is AnimatorStateMachine machine && machine != root) Object.DestroyImmediate(machine, true);
            else if (asset is AnimatorState orphan) Object.DestroyImmediate(orphan, true);
        }
    }
}
