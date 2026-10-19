using System.Linq;
using UnityEditor;
using UnityEngine;

// Sets how Unity imports the civilians' clips from NPC.fbx. The FBX arrives with no clip settings, so
// nothing loops and the root motion is not baked: a dwell clip would freeze on its last frame and a walk
// cycle would slide. Applied by CivilianLocomotionBuilder.Rebuild before it reads the clips, so one menu
// click is enough, and a no-op when the settings are already right (no reimport). The clip ranges are read
// fresh from the FBX each time, so a re-export with new lengths needs nothing else. Only the clips the
// graph uses get settings; the rest (the clerk's set, the reactions, the run) keep their defaults until
// something plays them
public static class CivilianClipImport
{
    public const string FbxPath = "Assets/NPC/Art/Civilians/Mesh/NPC.fbx";

    // The FBX prefixes every clip with its armature's name
    private const string ClipPrefix = "Armature|";

    // Every clip the graph plays loops: the legs, the dwell clips (a civilian stays 6 to 35 s at a spot,
    // longer than any clip) and the two phone poses, which hold until the window closes
    private static readonly string[] Cycles =
    {
        "NPC_Idle", "NPC_Walk",
        "Customer_BrowseShelf", "Customer_ClothesRack", "Customer_InspectTable", "Customer_LookShoes",
        "Employee_Register", "Employee_BinDump", "Employee_Restock", "Employee_TidyShelf",
        "Clerk_PhoneReach", "Clerk_PhoneCall",
    };

    public static void Apply()
    {
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[CivilianClipImport] No model at {FbxPath}.");
            return;
        }

        // Always from the take list Unity generates now, never from the ranges stored in the meta file:
        // a clip re-exported from Blender with a new length would otherwise keep its old frame range
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;

        foreach (ModelImporterClipAnimation clip in clips)
        {
            string name = clip.name.StartsWith(ClipPrefix) ? clip.name.Substring(ClipPrefix.Length) : clip.name;
            if (Cycles.Contains(name)) SetLooping(clip);
        }

        if (Same(importer.clipAnimations, clips)) return;

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        Debug.Log("[CivilianClipImport] Applied the clip import settings to NPC.fbx.");
    }

    // Rotation, height and position are all baked into the pose, because the NavMeshAgent moves the
    // character and NpcFacing turns it: the clips must not move it too (CLAUDE.md, Animation)
    private static void SetLooping(ModelImporterClipAnimation clip)
    {
        clip.loopTime = true;
        clip.loopPose = true;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = true;
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
