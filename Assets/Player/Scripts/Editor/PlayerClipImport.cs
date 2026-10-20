using System.Linq;
using UnityEditor;
using UnityEngine;

// Sets how Unity imports the player's clips from Player.fbx. The FBX arrives with no clip settings, so
// nothing loops and the root motion is not baked: a walk cycle in a blend tree would freeze on its last
// frame. Applied by PlayerLocomotionBuilder.Rebuild before it reads the clips, so one menu click is
// enough, and a no-op when the settings are already right (no reimport). The clip ranges are read fresh
// from the FBX each time, so a re-export with new clip lengths needs nothing else. Only the clips the
// graph uses get settings; the Cartoon clips and the default take keep their defaults
public static class PlayerClipImport
{
    public const string FbxPath = "Assets/Player/Art/Meshes/Player.fbx";

    // The FBX prefixes every clip with its armature's name
    private const string ClipPrefix = "PlayerArmature|";

    // Looping cycles: the blend-tree legs and the two idles (the reworked "_New" clips PlayerLocomotionBuilder uses)
    private static readonly string[] Cycles =
    {
        "Player_Idle_Stand_New", "Player_Idle_Crouch_New",
        "Player_Walk_Forward_New", "Player_Walk_Backward_New", "Player_Walk_Left_New", "Player_Walk_Right_New",
        "Player_Run_Forward_New", "Player_Run_Backward_New", "Player_Run_Left_New", "Player_Run_Right_New",
        "Player_Crouch_Forward_New", "Player_Crouch_Backward_New", "Player_Crouch_Left_New", "Player_Crouch_Right_New",
    };

    // Play once, then hand back: the random idle variants
    private static readonly string[] Breaks =
    {
        "Player_Idle_Stand_Impatient", "Player_Idle_Stand_Phone",
        "Player_Idle_Crouch_Peek", "Player_Idle_Crouch_Scheme",
    };

    // Turn-in-place: the code does the turning, so the clip's root rotation stays unbaked and the clip
    // supplies only the footwork (CLAUDE.md, Animation)
    private static readonly string[] Turns =
    {
        "Player_Turn_Left_Stand", "Player_Turn_Right_Stand",
        "Player_Turn_Left_Crouch", "Player_Turn_Right_Crouch",
    };

    // Plays once. The clip keeps the hips at standing height in the air, because PlayerMotor supplies the
    // whole rise; its height is based on Original so the tucked legs lift the feet instead of the body
    // sinking to keep them on the floor (CLAUDE.md, Animation)
    private const string Jump = "Player_Jump_New";

    public static void Apply()
    {
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[PlayerClipImport] No model at {FbxPath}.");
            return;
        }

        // Always from the take list Unity generates now, never from the ranges stored in the meta file:
        // a clip re-exported from Blender with a new length would otherwise keep its old frame range
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;

        foreach (ModelImporterClipAnimation clip in clips)
        {
            string name = clip.name.StartsWith(ClipPrefix) ? clip.name.Substring(ClipPrefix.Length) : clip.name;

            if (Cycles.Contains(name)) Set(clip, loop: true, bakeRotation: true);
            else if (Breaks.Contains(name)) Set(clip, loop: false, bakeRotation: true);
            else if (Turns.Contains(name)) Set(clip, loop: false, bakeRotation: false);
            else if (name == Jump) Set(clip, loop: false, bakeRotation: true, heightFromFeet: false);
        }

        if (Same(importer.clipAnimations, clips)) return;

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        Debug.Log("[PlayerClipImport] Applied the clip import settings to Player.fbx.");
    }

    // Position Y and XZ are always baked into the pose, because PlayerMotor moves the character and
    // the clips must not move it too. Height comes from the feet unless the clip leaves the ground
    private static void Set(ModelImporterClipAnimation clip, bool loop, bool bakeRotation, bool heightFromFeet = true)
    {
        clip.loopTime = loop;
        clip.loopPose = loop;
        clip.lockRootRotation = bakeRotation;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = true;
        clip.heightFromFeet = heightFromFeet;
        clip.keepOriginalOrientation = false;
        clip.keepOriginalPositionY = !heightFromFeet;
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
