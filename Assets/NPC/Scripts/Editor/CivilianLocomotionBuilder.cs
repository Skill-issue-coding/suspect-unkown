using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Rebuilds the civilians' animator from code via Tools > Suspect Unknown > NPC > Civilian > Rebuild
// Locomotion. Applies NPC.fbx's clip import settings first, then builds the base layer: Standing (a Speed
// tree, idle and walk) while moving between spots, and Dwell (a Station tree, one clip per CivilianStation)
// while standing at one, toggled by Dwelling. On top, the shared Phone layer (AnimatorBuildUtility) takes
// the phone out while the decision window is open. All clips are in NPC.fbx, each take prefixed with the
// armature's name
public static class CivilianLocomotionBuilder
{
    private const string ControllerPath = "Assets/NPC/Animators/CivilianAnimator.controller";
    private const string ClipPrefix = "Armature|";
    private const string LogPrefix = "[CivilianLocomotionBuilder]";

    // The walk is animated in place for about this speed; CivilianBehaviour's walk speed should match it,
    // or the feet slide
    private const float WalkSpeed = 1.2f;

    [MenuItem(MenuPaths.CivilianRebuildLocomotion, false, MenuPaths.NpcPriority + 2)]
    public static void Rebuild()
    {
        // Settings before reading: a clip read before the reimport is the old one
        CivilianClipImport.Apply();

        AnimationClip idle = Clip("NPC_Idle"), walk = Clip("NPC_Walk");

        // In CivilianStation order: index n is threshold n in the Dwell tree, so a wrong order plays the
        // wrong clip at a spot with no error
        AnimationClip[] dwell =
        {
            Clip("Customer_BrowseShelf"), Clip("Customer_ClothesRack"), Clip("Customer_InspectTable"), Clip("Customer_LookShoes"),
            Clip("Employee_Register"), Clip("Employee_BinDump"), Clip("Employee_Restock"), Clip("Employee_TidyShelf"),
        };

        // The clerk's phone clips, retargeted through the Humanoid avatar: every civilian reaches the same way
        AnimationClip reach = Clip("Clerk_PhoneReach"), call = Clip("Clerk_PhoneCall");

        if (!idle || !walk || !reach || !call || System.Array.Exists(dwell, clip => !clip))
        {
            Debug.LogError($"{LogPrefix} Missing clips in {CivilianClipImport.FbxPath}. Aborting.");
            return;
        }

        AnimatorController controller = PrepareController();
        AddParameters(controller);
        BuildBaseLayer(controller, idle, walk, dwell);
        AnimatorBuildUtility.AddPhoneLayer(controller, reach, call);

        AssetDatabase.SaveAssets();
        Debug.Log($"{LogPrefix} Rebuilt {ControllerPath}", controller);
        Selection.activeObject = controller;
    }

    // Reset in place rather than delete and recreate: a new asset gets a new GUID and would unhook the
    // Animator on every civilian prefab
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
        controller.AddParameter(CivilianAnimatorParams.Station, AnimatorControllerParameterType.Float);
        controller.AddParameter(CivilianAnimatorParams.Dwelling, AnimatorControllerParameterType.Bool);
        controller.AddParameter(NpcAnimatorParams.Deliberating, AnimatorControllerParameterType.Bool);
        controller.AddParameter(NpcAnimatorParams.Calling, AnimatorControllerParameterType.Bool);
    }

    // Walking between spots, then the spot's clip while standing at it. Dwelling toggles between the two;
    // Station only changes while walking, so the Dwell tree never visibly blends two clips
    private static void BuildBaseLayer(AnimatorController controller, AnimationClip idle, AnimationClip walk,
        AnimationClip[] dwell)
    {
        AnimatorStateMachine root = controller.layers[0].stateMachine;

        AnimatorState standing = AnimatorBuildUtility.AddState(root, "Standing", Standing(controller, idle, walk));
        AnimatorState dwelling = AnimatorBuildUtility.AddState(root, "Dwell", Dwell(controller, dwell));
        root.defaultState = standing;

        AnimatorBuildUtility.AddToggleTransitions(root, standing, dwelling, CivilianAnimatorParams.Dwelling,
            fromAnyState: false);
    }

    // Standing still at 0 m/s, the walk at its own speed. A civilian that stops stops its legs
    private static Motion Standing(AnimatorController controller, AnimationClip idle, AnimationClip walk)
    {
        BlendTree tree = AnimatorBuildUtility.CreateTree(controller, "Standing", NpcAnimatorParams.Speed);
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, WalkSpeed);
        return tree;
    }

    // One clip per CivilianStation, clip n at threshold n
    private static Motion Dwell(AnimatorController controller, AnimationClip[] clipsByStation)
    {
        BlendTree tree = AnimatorBuildUtility.CreateTree(controller, "Dwell", CivilianAnimatorParams.Station);
        for (int i = 0; i < clipsByStation.Length; i++)
            tree.AddChild(clipsByStation[i], i);
        return tree;
    }

    private static AnimationClip Clip(string name)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CivilianClipImport.FbxPath))
            if (asset is AnimationClip clip && clip.name == ClipPrefix + name) return clip;
        Debug.LogWarning($"{LogPrefix} No clip {name} in {CivilianClipImport.FbxPath}");
        return null;
    }
}
