using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Rebuilds the apartment clerk's animator from code via Tools > Suspect Unknown > NPC > Clerk > Rebuild
// Locomotion. The base layer follows ResidentPhase: asleep, stirring and awake in the armchair, standing
// up, walking or standing still on the snack run (a Speed tree), snacking, and sitting back down. An
// upper-body layer, masked to the head and arms, takes the phone out while deliberating and to the ear
// while calling, so the same clips work seated and standing. All clips are in Clerk_Animations.fbx
public static class ClerkLocomotionBuilder
{
    private const string ControllerPath = "Assets/NPC/Animators/ClerkAnimator.controller";
    private const string MaskPath = "Assets/NPC/Animators/ClerkUpperBody.mask";
    private const string ClipFile = "Assets/NPC/Art/Clerk/Animations/Clerk_Animations.fbx";
    private const string LogPrefix = "[ClerkLocomotionBuilder]";

    // The walk is animated in place for about this speed; ResidentBehaviour's walk speed should match it,
    // or the feet slide
    private const float WalkSpeed = 1.2f;

    [MenuItem(MenuPaths.ClerkRebuildLocomotion, false, MenuPaths.NpcPriority + 1)]
    public static void Rebuild()
    {
        AnimationClip sleep = Clip("Clerk_SitSleep"), stir = Clip("Clerk_SitStir"), awake = Clip("Clerk_SitAwake");
        AnimationClip standUp = Clip("Clerk_StandUp"), sitDown = Clip("Clerk_SitDown"), idle = Clip("Clerk_StandIdle");
        AnimationClip walk = Clip("Clerk_WalkTired"), snack = Clip("Clerk_Snack");
        AnimationClip reach = Clip("Clerk_PhoneReach"), call = Clip("Clerk_PhoneCall");
        if (!sleep || !stir || !awake || !standUp || !sitDown || !idle || !walk || !snack || !reach || !call)
        {
            Debug.LogError($"{LogPrefix} Missing clips in {ClipFile}. Aborting.");
            return;
        }

        AnimatorController controller = PrepareController();
        AddParameters(controller);
        BuildBaseLayer(controller, sleep, stir, awake, standUp, sitDown, idle, walk, snack);
        BuildPhoneLayer(controller, reach, call);

        AssetDatabase.SaveAssets();
        Debug.Log($"{LogPrefix} Rebuilt {ControllerPath}", controller);
        Selection.activeObject = controller;
    }

    private static AnimationClip Clip(string name)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ClipFile))
            if (asset is AnimationClip clip && clip.name == name) return clip;
        Debug.LogWarning($"{LogPrefix} No clip {name} in {ClipFile}");
        return null;
    }

    // Reset in place rather than delete and recreate: a new asset gets a new GUID and would unhook the
    // clerk's Animator
    private static AnimatorController PrepareController()
    {
        AnimatorBuildUtility.EnsureFolder(Path.GetDirectoryName(ControllerPath));
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) return AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorBuildUtility.ResetController(controller, ControllerPath);
        return controller;
    }

    // TurnRate is unused by this graph, but NpcAnimator sets it on every NPC; without it the Animator
    // warns every frame
    private static void AddParameters(AnimatorController controller)
    {
        controller.AddParameter(NpcAnimatorParams.Speed, AnimatorControllerParameterType.Float);
        controller.AddParameter(NpcAnimatorParams.TurnRate, AnimatorControllerParameterType.Float);
        controller.AddParameter(ResidentAnimatorParams.Phase, AnimatorControllerParameterType.Int);
        controller.AddParameter(ResidentAnimatorParams.Scared, AnimatorControllerParameterType.Bool);
        controller.AddParameter(ResidentAnimatorParams.Deliberating, AnimatorControllerParameterType.Bool);
        controller.AddParameter(ResidentAnimatorParams.Calling, AnimatorControllerParameterType.Bool);
    }

    private static void BuildBaseLayer(AnimatorController controller, AnimationClip sleep, AnimationClip stir,
        AnimationClip awake, AnimationClip standUp, AnimationClip sitDown, AnimationClip idle, AnimationClip walk,
        AnimationClip snack)
    {
        AnimatorStateMachine root = controller.layers[0].stateMachine;

        AnimatorState sleeping = AnimatorBuildUtility.AddState(root, "Sleep", sleep);
        AnimatorState stirring = AnimatorBuildUtility.AddState(root, "Stir", stir);
        AnimatorState sitting = AnimatorBuildUtility.AddState(root, "SitAwake", awake);
        AnimatorState rising = AnimatorBuildUtility.AddState(root, "StandUp", standUp);
        AnimatorState standing = AnimatorBuildUtility.AddState(root, "Standing", Standing(controller, idle, walk));
        AnimatorState snacking = AnimatorBuildUtility.AddState(root, "Snack", snack);
        AnimatorState sittingDown = AnimatorBuildUtility.AddState(root, "SitDown", sitDown);
        root.defaultState = sleeping;

        // In the armchair
        OnPhase(sleeping, stirring, ResidentPhase.Stirring, 0.4f);
        OnPhase(sleeping, sitting, ResidentPhase.Awake, 0.3f);         // woken by a scare: no yawn
        OnPhase(sleeping, rising, ResidentPhase.GettingUp, 0.4f);       // the snack run starts from a doze
        OnPhase(stirring, sleeping, ResidentPhase.Dozing, 0.8f);        // dozed off again before the yawn ended
        AfterClip(stirring, sitting, 0.4f);
        OnPhase(sitting, sleeping, ResidentPhase.Dozing, 0.8f);

        // The snack run
        AfterClip(rising, standing, 0.3f);
        Transition(standing, snacking, 0.4f,
            (AnimatorConditionMode.Equals, (int)ResidentPhase.Snacking, ResidentAnimatorParams.Phase),
            (AnimatorConditionMode.IfNot, 0, ResidentAnimatorParams.Scared));
        Transition(snacking, standing, 0.4f, (AnimatorConditionMode.NotEqual, (int)ResidentPhase.Snacking, ResidentAnimatorParams.Phase));
        Transition(snacking, standing, 0.3f, (AnimatorConditionMode.If, 0, ResidentAnimatorParams.Scared));
        OnPhase(standing, sittingDown, ResidentPhase.Awake, 0.3f);      // back at the armchair
        AfterClip(sittingDown, sitting, 0.4f);
    }

    // Standing still at 0 m/s, the tired walk at its own speed. A scare that stops the agent stops the legs
    private static Motion Standing(AnimatorController controller, AnimationClip idle, AnimationClip walk)
    {
        BlendTree tree = AnimatorBuildUtility.CreateTree(controller, "Standing", NpcAnimatorParams.Speed);
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, WalkSpeed);
        return tree;
    }

    // Head and arms only, so the phone clips sit on top of whatever the legs and spine are doing. Weight 0
    // until ResidentAnimation fades it in: on a Humanoid, a layer at full weight with no clip playing still
    // writes the neutral muscle pose for every masked part (arms raised, head stiff)
    private static void BuildPhoneLayer(AnimatorController controller, AnimationClip reach, AnimationClip call)
    {
        AnimatorStateMachine machine = new AnimatorStateMachine { name = "Phone", hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(machine, controller);
        controller.AddLayer(new AnimatorControllerLayer
        {
            name = "Phone",
            stateMachine = machine,
            avatarMask = UpperBodyMask(),
            blendingMode = AnimatorLayerBlendingMode.Override,
            defaultWeight = 0f,
        });

        // Where the layer rests while its weight is 0
        AnimatorState none = AnimatorBuildUtility.AddState(machine, "None", null);
        AnimatorState reaching = AnimatorBuildUtility.AddState(machine, "PhoneReach", reach);
        AnimatorState calling = AnimatorBuildUtility.AddState(machine, "PhoneCall", call);
        machine.defaultState = none;

        Transition(none, reaching, 0.35f, (AnimatorConditionMode.If, 0, ResidentAnimatorParams.Deliberating));
        Transition(none, calling, 0.35f, (AnimatorConditionMode.If, 0, ResidentAnimatorParams.Calling));
        Transition(reaching, calling, 0.4f, (AnimatorConditionMode.If, 0, ResidentAnimatorParams.Calling));
        Transition(reaching, none, 0.4f,
            (AnimatorConditionMode.IfNot, 0, ResidentAnimatorParams.Deliberating),
            (AnimatorConditionMode.IfNot, 0, ResidentAnimatorParams.Calling));
    }

    // Updated in place, like the controller, so its GUID survives a rebuild
    private static AvatarMask UpperBodyMask()
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, MaskPath);
        }

        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        EditorUtility.SetDirty(mask);
        return mask;
    }

    private static void OnPhase(AnimatorState from, AnimatorState to, ResidentPhase phase, float duration) =>
        Transition(from, to, duration, (AnimatorConditionMode.Equals, (int)phase, ResidentAnimatorParams.Phase));

    // Plays a one-shot nearly to its end, then blends on
    private static void AfterClip(AnimatorState from, AnimatorState to, float duration)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        AnimatorBuildUtility.Configure(transition, duration, hasExitTime: true, exitTime: 0.92f);
    }

    private static void Transition(AnimatorState from, AnimatorState to, float duration,
        params (AnimatorConditionMode mode, float threshold, string parameter)[] conditions)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        foreach (var (mode, threshold, parameter) in conditions)
            transition.AddCondition(mode, threshold, parameter);
        AnimatorBuildUtility.Configure(transition, duration, hasExitTime: false);
    }
}
