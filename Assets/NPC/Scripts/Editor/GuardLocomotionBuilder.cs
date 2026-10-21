using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Rebuilds the guard's animator from code via Tools > Suspect Unknown > NPC > Guard > Rebuild Locomotion.
// Clips are grouped into stances (ClipSet); a stance with several clips becomes a variant tree the runtime
// picks from. Standing stances step into turn clips on TurnRate, in degrees per second, positive right.
// Locomotion is a Speed tree in metres per second: GuardBehaviour's speeds must match the logged values,
// or the feet slide. Turn clips must keep root rotation unbaked.
public static class GuardLocomotionBuilder
{
    private const string ControllerPath = "Assets/NPC/Animators/GuardAnimator.controller";
    private const string LogPrefix = "[GuardLocomotionBuilder]";

    [MenuItem(MenuPaths.GuardRebuildLocomotion, false, MenuPaths.NpcPriority)]
    public static void Rebuild()
    {
        // Settings before reading: a clip read before the reimport is the old one
        NpcClipImport.Apply();

        GuardClips clips = GuardClips.Load();
        if (!clips.HasRequired)
        {
            Debug.LogError($"{LogPrefix} Missing the patrol idle or walk. Aborting.");
            return;
        }

        AnimatorController controller = PrepareController();
        AddParameters(controller);

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorState locomotion = AnimatorBuildUtility.AddState(root, "Locomotion", Locomotion(controller, clips));
        root.defaultState = locomotion;

        if (!clips.Watch.IsEmpty)
            AnimatorBuildUtility.AddToggleTransitions(root, locomotion,
                AnimatorBuildUtility.AddState(root, GuardAnimatorParams.Watching, Stand(controller, clips.Watch, clips.Turn)),
                GuardAnimatorParams.Watching, fromAnyState: true);

        if (!clips.Search.IsEmpty)
            AnimatorBuildUtility.AddToggleTransitions(root, locomotion,
                AnimatorBuildUtility.AddState(root, GuardAnimatorParams.Searching, Variants(controller, clips.Search)),
                GuardAnimatorParams.Searching, fromAnyState: false);

        if (!clips.GiveUp.IsEmpty)
            AnimatorBuildUtility.AddOneShotTransitions(root, locomotion,
                AnimatorBuildUtility.AddState(root, GuardAnimatorParams.GiveUp, Variants(controller, clips.GiveUp)),
                GuardAnimatorParams.GiveUp);

        // The confused beat at an open door or safe, ending in the slam. One clip for every anomaly the guard
        // resolves; GuardBehaviour's confusedDuration is timed to the clip's impact
        if (!clips.Slam.IsEmpty)
            AnimatorBuildUtility.AddOneShotTransitions(root, locomotion,
                AnimatorBuildUtility.AddState(root, GuardAnimatorParams.Slam, Variants(controller, clips.Slam)),
                GuardAnimatorParams.Slam);

        WarnOnVariantCount(clips.Idle, GuardAnimatorParams.IdleVariantCount);
        WarnOnVariantCount(clips.GiveUp, GuardAnimatorParams.GiveUpVariantCount);

        AssetDatabase.SaveAssets();
        Debug.Log($"{LogPrefix} Rebuilt {ControllerPath}", controller);
        Selection.activeObject = controller;
    }

    // Reset in place rather than delete and recreate: a new asset gets a new GUID and would
    // silently unhook the Animator on every guard prefab.
    private static AnimatorController PrepareController()
    {
        AnimatorBuildUtility.EnsureFolder(Path.GetDirectoryName(ControllerPath));

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) return AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorBuildUtility.ResetController(controller, ControllerPath);
        return controller;
    }

    private static void AddParameters(AnimatorController controller)
    {
        controller.AddParameter(NpcAnimatorParams.Speed, AnimatorControllerParameterType.Float);
        controller.AddParameter(NpcAnimatorParams.TurnRate, AnimatorControllerParameterType.Float);
        controller.AddParameter(GuardAnimatorParams.Watching, AnimatorControllerParameterType.Bool);
        controller.AddParameter(GuardAnimatorParams.Searching, AnimatorControllerParameterType.Bool);
        controller.AddParameter(GuardAnimatorParams.IdleVariant, AnimatorControllerParameterType.Float);
        controller.AddParameter(GuardAnimatorParams.GiveUp, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(GuardAnimatorParams.GiveUpVariant, AnimatorControllerParameterType.Float);
        controller.AddParameter(GuardAnimatorParams.Slam, AnimatorControllerParameterType.Trigger);
    }

    // Speed tree in metres per second: standing (with turns) at 0, then each gait at its own speed.
    // Children must be added in ascending threshold order.
    private static Motion Locomotion(AnimatorController controller, GuardClips clips)
    {
        BlendTree tree = AnimatorBuildUtility.CreateTree(controller, "Locomotion", NpcAnimatorParams.Speed);

        tree.AddChild(Stand(controller, clips.Idle, clips.Turn), 0f);
        AddGait(tree, clips.PatrolWalk);
        AddGait(tree, clips.InvestigateWalk);
        AddGait(tree, clips.Run);

        Debug.Log($"{LogPrefix} Speeds: " +
             string.Join(", ", tree.children.Skip(1).Select(c => $"{c.motion.name} {c.threshold:0.00} m/s")));
        return tree;
    }

    private static void AddGait(BlendTree tree, Gait gait) { if (!gait.IsEmpty) tree.AddChild(gait.Clip, gait.Speed); }

    // A standing stance that steps into a turn clip when the body rotates in place. TurnRate is signed:
    // negative left, positive right. At twice a clip's own rate it plays twice as fast, so the feet keep
    // up with quick turns. Without both turn clips, the stance is just its pose.
    private static Motion Stand(AnimatorController controller, ClipSet set, TurnSet turn)
    {
        Motion pose = Variants(controller, set);
        if (!turn.IsComplete) return pose;

        BlendTree tree = AnimatorBuildUtility.CreateTree(controller, $"{set.Name} Stand", NpcAnimatorParams.TurnRate);
        tree.AddChild(turn.Left, -2f * turn.LeftRate);
        tree.AddChild(turn.Left, -turn.LeftRate);
        tree.AddChild(pose, 0f);
        tree.AddChild(turn.Right, turn.RightRate);
        tree.AddChild(turn.Right, 2f * turn.RightRate);

        ChildMotion[] children = tree.children;
        children[0].timeScale = 2f;
        children[4].timeScale = 2f;
        tree.children = children;

        return tree;
    }

    // A stance as a motion: its one clip, or a 1D tree over its variants with clip n at threshold n.
    private static Motion Variants(AnimatorController controller, ClipSet set)
    {
        if (!set.HasVariants) return set.Clips[0];

        if (set.VariantParameter == null)
        {
            Debug.LogWarning($"{LogPrefix} {set.Name} has {set.Clips.Length} clips but no variant parameter; using the first.");
            return set.Clips[0];
        }

        BlendTree tree = AnimatorBuildUtility.CreateTree(controller, set.Name, set.VariantParameter);
        for (int i = 0; i < set.Clips.Length; i++)
            tree.AddChild(set.Clips[i], i);
        return tree;
    }

    // The runtime picks a variant within the count in GuardAnimatorParams. A mismatch would make it pick
    // a clip that doesn't exist (too few clips) or never pick one that does (too many).
    private static void WarnOnVariantCount(ClipSet set, int expected)
    {
        if (set.Clips.Length == expected) return;

        Debug.LogWarning($"{LogPrefix} {set.Name}: {set.Clips.Length} clips, " +
                         $"GuardAnimatorParams expects {expected}.");
    }
}
