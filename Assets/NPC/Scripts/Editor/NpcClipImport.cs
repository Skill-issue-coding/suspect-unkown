using System.Linq;
using UnityEditor;
using UnityEngine;

// Sets how Unity imports the NPC clips in NPC.fbx, the civilians' and the guard's, and loads them for the
// builders. The FBX arrives with no clip settings, so nothing loops and the root motion is not baked: a dwell
// clip would freeze on its last frame and a walk cycle would slide. Applied by CivilianLocomotionBuilder and
// GuardLocomotionBuilder before they read the clips, so one menu click is enough, and a no-op when the
// settings are already right (no reimport). The clip ranges are read fresh from the FBX each time, so a
// re-export with new lengths or new takes needs nothing else. Only the clips a graph uses get settings; the
// rest (the clerk's set, React_BackAway, React_Startled, NPC_Run) keep their defaults until
// something plays them
public static class NpcClipImport
{
    public const string FbxPath = "Assets/NPC/Art/Civilians/Mesh/NPC.fbx";

    // The FBX prefixes every clip with its armature's name
    private const string ClipPrefix = "Armature|";

    // Clips that loop and bake all root motion into the pose: the legs, the dwell clips (a civilian stays 6
    // to 35 s at a spot, longer than any clip), the two phone poses, which hold until the window closes, the
    // two reactions that last longer than their clip (the 8 to 10 s scare, the walk away), and the guard's
    // standing stances
    private static readonly string[] Cycles =
    {
        "NPC_Idle", "NPC_Walk",
        "Customer_BrowseShelf", "Customer_ClothesRack", "Customer_InspectTable", "Customer_LookShoes",
        "Employee_Register", "Employee_BinDump", "Employee_Restock", "Employee_TidyShelf",
        "Clerk_PhoneReach", "Clerk_PhoneCall",
        "React_Scared", "React_AvoidWalk",
        "Guard_Idle_1", "Guard_Idle_2", "Guard_Watch", "Guard_Search",
    };

    // Played once: held on the last frame while the state lasts (the decision window can run longer than
    // the clip, and a loop would snap the pose back to its start), or left near the end (the give-ups and
    // the guard's slam)
    private static readonly string[] Holds =
        { "React_Deliberating", "Guard_GiveUp_1", "Guard_GiveUp_2", "Guard_DoorSlam" };

    // The guard's gaits loop with Position XZ unbaked, so GuardClips can measure each gait's speed from its
    // root motion. Harmless at runtime: the guard's Animator applies no root motion
    private static readonly string[] Gaits = { "Guard_Walk", "Guard_InvestigateWalk", "Guard_Run" };

    // Turn-in-place clips loop with Root Rotation unbaked, so TurnSet can measure their rate and the code,
    // not the clip, does the turning (CLAUDE.md, Animation)
    private static readonly string[] Turns = { "Guard_TurnLeft", "Guard_TurnRight" };

    public static void Apply()
    {
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[NpcClipImport] No model at {FbxPath}.");
            return;
        }

        // Always from the take list Unity generates now, never from the ranges stored in the meta file:
        // a clip re-exported from Blender with a new length would otherwise keep its old frame range
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;

        foreach (ModelImporterClipAnimation clip in clips)
        {
            string name = clip.name.StartsWith(ClipPrefix) ? clip.name.Substring(ClipPrefix.Length) : clip.name;
            if (Cycles.Contains(name)) Bake(clip, loop: true);
            else if (Holds.Contains(name)) Bake(clip, loop: false);
            else if (Gaits.Contains(name)) Bake(clip, loop: true, bakePositionXZ: false);
            else if (Turns.Contains(name)) Bake(clip, loop: true, bakeRotation: false);
        }

        if (Same(importer.clipAnimations, clips)) return;

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        Debug.Log("[NpcClipImport] Applied the clip import settings to NPC.fbx.");
    }

    // A clip from NPC.fbx by its take name without the armature prefix, or null with a warning
    public static AnimationClip Load(string name)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            if (asset is AnimationClip clip && clip.name == ClipPrefix + name) return clip;

        Debug.LogWarning($"[NpcClipImport] No clip {name} in {FbxPath}");
        return null;
    }

    // Rotation, height and position are baked into the pose by default, because the NavMeshAgent moves the
    // character and NpcFacing turns it: the clips must not move it too (CLAUDE.md, Animation). Gaits and
    // turns leave one part unbaked only so the builder can measure it
    private static void Bake(ModelImporterClipAnimation clip, bool loop, bool bakeRotation = true,
        bool bakePositionXZ = true)
    {
        clip.loopTime = loop;
        clip.loopPose = loop;
        clip.lockRootRotation = bakeRotation;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = bakePositionXZ;
        clip.heightFromFeet = true;
        clip.keepOriginalOrientation = false;
        clip.keepOriginalPositionY = false;
        clip.keepOriginalPositionXZ = false;
    }

    // Whether the stored settings already match, so a rebuild with nothing new does not reimport
    private static bool Same(ModelImporterClipAnimation[] stored, ModelImporterClipAnimation[] wanted)
    {
        if (stored.Length != wanted.Length) return false;

        for (int i = 0; i < wanted.Length; i++)
        {
            ModelImporterClipAnimation a = stored[i], b = wanted[i];
            if (a.name != b.name || a.firstFrame != b.firstFrame || a.lastFrame != b.lastFrame
                || a.loopTime != b.loopTime || a.loopPose != b.loopPose
                || a.lockRootRotation != b.lockRootRotation || a.lockRootHeightY != b.lockRootHeightY
                || a.lockRootPositionXZ != b.lockRootPositionXZ || a.heightFromFeet != b.heightFromFeet
                || a.keepOriginalOrientation != b.keepOriginalOrientation
                || a.keepOriginalPositionY != b.keepOriginalPositionY
                || a.keepOriginalPositionXZ != b.keepOriginalPositionXZ)
                return false;
        }

        return true;
    }
}
