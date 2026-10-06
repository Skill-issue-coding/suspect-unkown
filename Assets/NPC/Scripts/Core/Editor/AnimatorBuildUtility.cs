using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Building blocks for code-built animator controllers: loading and measuring clips, creating trees,
// states and transitions, and resetting a controller in place. Nothing here knows about a specific
// character; the builders decide the graph.
public static class AnimatorBuildUtility
{
    private const string LogPrefix = "[AnimatorBuild]";

    // Below this, a measured speed is treated as "no root motion" rather than a very slow clip
    private const float MinMeasurableSpeed = 0.05f;
    private const float MinMeasurableTurnRate = 1f;

    public static AnimationClip LoadClip(string root, string fileName)
    {
        string path = $"{root}/{fileName}";

        // An imported FBX holds its clip as a sub-asset, alongside a hidden __preview__ copy.
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        if (clip == null) Debug.LogWarning($"{LogPrefix} No clip at {path}");
        return clip;
    }

    // averageSpeed is the clip's root motion in metres per second. A Humanoid clip exported with
    // "In Place" unchecked has it; an in-place clip reads zero and gets the fallback
    public static float MeasureSpeed(AnimationClip clip, float fallback)
    {
        float speed = new Vector3(clip.averageSpeed.x, 0.0f, clip.averageSpeed.z).magnitude;
        if (speed >= MinMeasurableSpeed) return speed;

        Debug.LogWarning($"{LogPrefix} {clip.name} has no root motion; using {fallback} m/s. " +
                        "Expect foot sliding until it matches the clip.");

        return fallback;
    }

    // Degrees per second the clip turns on its own. Only readable when Root Transform Rotation is not
    // baked; a baked clip reads zero and gets the fallback.
    public static float MeasureTurnRate(AnimationClip clip, float fallback)
    {
        float rate = Mathf.Abs(clip.averageAngularSpeed);
        if (rate >= MinMeasurableTurnRate) return rate;

        Debug.LogWarning($"{LogPrefix} {clip.name} has no root rotation; using {fallback}°/s. " +
                     "Untick Bake Into Pose for Root Transform Rotation on turn clips.");
        return fallback;
    }

    public static void EnsureFolder(string folder)
    {
        folder = folder.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    // Strips the controller back to a single empty layer, keeping the asset (and its GUID) alive.
    public static void ResetController(AnimatorController controller, string path)
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
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is BlendTree tree) Object.DestroyImmediate(tree, true);
            else if (asset is AnimatorStateMachine machine && machine != root) Object.DestroyImmediate(machine, true);
            else if (asset is AnimatorState orphan) Object.DestroyImmediate(orphan, true);
        }
    }

    public static BlendTree CreateTree(AnimatorController controller, string name, string parameter)
    {
        BlendTree tree = new BlendTree
        {
            name = name,
            blendType = BlendTreeType.Simple1D,
            blendParameter = parameter,
            useAutomaticThresholds = false,
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    public static AnimatorState AddState(AnimatorStateMachine root, string name, Motion motion)
    {
        AnimatorState state = root.AddState(name);
        state.motion = motion;
        state.writeDefaultValues = false;
        return state;
    }

    // Entered while a bool is true, left when it turns false. Either direction can reverse mid-blend.
    public static void AddToggleTransitions(AnimatorStateMachine root, AnimatorState home, AnimatorState state,
        string parameter, bool fromAnyState, float duration = 0.3f)
    {
        AnimatorStateTransition enter = fromAnyState ? root.AddAnyStateTransition(state) : home.AddTransition(state);
        enter.AddCondition(AnimatorConditionMode.If, 0f, parameter);
        enter.canTransitionToSelf = false;
        Configure(enter, duration, hasExitTime: false);

        AnimatorStateTransition exit = state.AddTransition(home);
        exit.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter);
        Configure(exit, duration, hasExitTime: false);
    }

    // Entered on a trigger from anywhere, plays nearly to the end, then blends home.
    public static void AddOneShotTransitions(AnimatorStateMachine root, AnimatorState home, AnimatorState state,
        string trigger, float enterDuration = 0.25f, float exitDuration = 0.3f, float exitTime = 0.9f)
    {
        AnimatorStateTransition enter = root.AddAnyStateTransition(state);
        enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        enter.canTransitionToSelf = false;
        Configure(enter, enterDuration, hasExitTime: false);

        AnimatorStateTransition exit = state.AddTransition(home);
        Configure(exit, exitDuration, hasExitTime: true, exitTime);
    }

    // Shared setup for every code-built transition. Fixed duration means seconds, not a fraction of the
    // clip, so a blend takes the same time whatever the clip length. Interruption by the destination lets
    // a transition reverse mid-blend when its condition flips back, instead of finishing and then popping.
    private static void Configure(AnimatorStateTransition transition, float duration, bool hasExitTime, float exitTime = 0f)
    {
        transition.duration = duration;
        transition.hasFixedDuration = true;
        transition.hasExitTime = hasExitTime;
        transition.exitTime = exitTime;
        transition.interruptionSource = TransitionInterruptionSource.Destination;
        transition.orderedInterruption = true;
    }
}
