using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Adds a PerceptionPoint to every bone BoxCollider in Player.prefab via
// Tools > Suspect Unknown > Player > Map Perception Points. Authoring it in code rather than by
// hand keeps the region assignments reviewable in git, and one click to reapply.
// Edits the prefab asset directly, not a scene instance, so no scene gets prefab overrides.
// There is no undo: commit or stash Player.prefab before running.
public static class MapPlayerPerceptionPoints
{
    private const string PlayerPrefabPath = "Assets/Player/Player.prefab";

    // Bone name to body region. Every bone collider becomes a perception point; 
    // archetypes decide which regions matter.
    private static readonly Dictionary<string, BodyRegion> Bones = new()
    {
        // Head
        { "Head",        BodyRegion.Head },
        { "Neck",        BodyRegion.Head },

        // Upper body
        { "Spine_01",    BodyRegion.UpperBody },
        { "Spine_02",    BodyRegion.UpperBody },
        { "Spine_03",    BodyRegion.UpperBody },
        { "Shoulder_L",  BodyRegion.UpperBody },
        { "Shoulder_R",  BodyRegion.UpperBody },
        { "Elbow_L",     BodyRegion.UpperBody },
        { "Elbow_R",     BodyRegion.UpperBody },

        // Hands
        { "Hand_L",      BodyRegion.Hands },
        { "Hand_R",      BodyRegion.Hands },

        // Lower body
        { "UpperLeg_L",  BodyRegion.LowerBody },
        { "UpperLeg_R",  BodyRegion.LowerBody },
        { "LowerLeg_L",  BodyRegion.LowerBody },
        { "LowerLeg_R",  BodyRegion.LowerBody },

        // Feet
        { "Ball_L",      BodyRegion.Feet },
        { "Ball_R",      BodyRegion.Feet },
    };

    // Serialized field names on PerceptionPoint. Renaming a field there means renaming it here,
    // or FindProperty returns null.
    private const string RegionField = "region";

    [MenuItem(MenuPaths.PlayerMapPerceptionPoints, false, MenuPaths.PlayerPriority + 1)]
    private static void Assign()
    {
        var found = new HashSet<string>();

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            if (root.GetComponent<PerceptionTargets>() == null)
                root.AddComponent<PerceptionTargets>();

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                // Bone colliders are boxes. This skips the CharacterController and the PlayerNoise sphere.
                if (collider is not BoxCollider)
                    continue;

                if (!Bones.TryGetValue(collider.name, out BodyRegion region))
                {
                    Debug.LogWarning($"No region for collider '{collider.name}'.", collider);
                    continue;
                }

                found.Add(collider.name);

                PerceptionPoint point = collider.GetComponent<PerceptionPoint>();
                if (point == null)
                    point = collider.gameObject.AddComponent<PerceptionPoint>();

                var so = new SerializedObject(point);
                so.FindProperty(RegionField).enumValueIndex = (int)region;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // A renamed or deleted bone would otherwise drop out of its region silently.
        foreach (string bone in Bones.Keys)
        {
            if (!found.Contains(bone))
                Debug.LogWarning($"Bone '{bone}' in the table has no BoxCollider in {PlayerPrefabPath}.");
        }

        Debug.Log($"Mapped {found.Count} of {Bones.Count} perception points in {PlayerPrefabPath}.");
    }
}
