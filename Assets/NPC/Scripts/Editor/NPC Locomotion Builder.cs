using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Rebuilds the guard locomotion graph from scratch via Tools > Suspect Unknown > NPC > Rebuild Locomotion.
// Guards never strafe: GuardBehaviour turns the body toward where it is heading, so one 1D tree on
// Speed (idle, walk, run) is the whole graph. Thresholds are in metres per second, measured from the
// clips themselves, so NpcAnimator feeds the agent's speed straight in and the feet match the ground.
public static class NPCLocomotionBuilder
{
    private const string ControllerPath = "Assets/NPC/Animation/GuardAnimator.controller";
    private const string ClipsRoot = "Assets/NPC/Art/Guard/Animation Clips";

    // Used when a clip was exported "In Place" and carries no root motion to measure.
    // Keep them near GuardBehaviour's patrol and pursue speeds.
    private const float FallbackRunSpeed = 4.5f;

    // Below this, a measured speed is treated as "no root motion" rather than a very slow clip.
    private const float MinMeasurableSpeed = 0.05f;

    [MenuItem(MenuPaths.NpcRebuildLocomotion, false, MenuPaths.NpcPriority)]
    public static void Rebuild()
    {
        AnimationClip idle = LoadClip("guard_idle.fbx");
        AnimationClip walk = LoadClip("guard_walk.fbx");
        AnimationClip run = LoadClip("guard_run_forward.fbx");

        if (idle == null || walk == null)
        {
            Debug.LogError("[NpcLocomotionBuilder] Missing guard_idle or guard_walk. Aborting.");
            return;
        }

        EnsureFolder(Path.GetDirectoryName(ControllerPath));

        // Reset in place rather than delete and recreate: a new asset gets a new GUID and would
        // silently unhook the Animator on every guard prefab.
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        else ResetController(controller);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorState locomotion = BuildLocomotion(controller, idle, walk, run);
        locomotion.writeDefaultValues = false;
        root.defaultState = locomotion;

        AssetDatabase.SaveAssets();
        Debug.Log($"[NpcLocomotionBuilder] Rebuilt {ControllerPath}", controller);
        Selection.activeObject = controller;
    }

    private static AnimatorState BuildLocomotion(AnimatorController controller, AnimationClip idle,
        AnimationClip walk, AnimationClip run)
    {
        AnimatorState state = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;

        float walkSpeed = MeasureSpeed(run, FallbackRunSpeed);

        tree.AddChild(idle, 0.0f);
        tree.AddChild(walk, walkSpeed);

        if (run != null)
            tree.AddChild(run, MeasureSpeed(run, FallbackRunSpeed));
        else
        {
            // No run clip: the walk played faster, at the rate that keeps its feet planted at run speed.
            Debug.LogWarning("[NpcLocomotionBuilder] No run clip; pursuit reuses the walk clip sped up.");
            tree.AddChild(walk, FallbackRunSpeed);
            ChildMotion[] children = tree.children;
            children[2].timeScale = FallbackRunSpeed / walkSpeed;
            tree.children = children;
        }

        Debug.Log($"[NpcLocomotionBuilder] Thresholds: " +
                  string.Join(", ", tree.children.Select(c => $"{c.motion.name} {c.threshold:0.00} m/s")));

        return state;
    }

    // averageSpeed is the clip's root motion in metres per second. A Humanoid clip exported with
    // "In Place" unchecked has it; an in-place clip reads zero and gets the fallback.
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
