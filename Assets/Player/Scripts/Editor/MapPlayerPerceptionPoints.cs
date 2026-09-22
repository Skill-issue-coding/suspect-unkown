using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Adds a PerceptionPoint to every bone BoxCollider in Player.prefab via
// Tools > Suspect Unknown > Player > Map Perception Points. Authoring it in code rather than by
// hand keeps the region and priority assignments reviewable in git, and one click to reapply.
// Edits the prefab asset directly, not a scene instance, so no scene gets prefab overrides.
// There is no undo: commit or stash Player.prefab before running.
public static class MapPlayerPerceptionPoints
{
    private const string PlayerPrefabPath = "Assets/Player/Player.prefab";

    // Bone name to region and priority. Priority 1 is the cast point for its region;
    // PerceptionTargets keeps only the highest-priority point per region.
    // Names must match the bone GameObjects in Player.prefab exactly.
    private static readonly Dictionary<string, (BodyRegion region, int priority)> Bones = new()
    {
        // Head
        { "Head",        (BodyRegion.Head, 1) },
        { "Neck",        (BodyRegion.Head, 0) },

        // Upper body
        { "Spine_02",    (BodyRegion.UpperBody, 1) },
        { "Spine_01",    (BodyRegion.UpperBody, 0) },
        { "Spine_03",    (BodyRegion.UpperBody, 0) },
        { "Shoulder_L",  (BodyRegion.UpperBody, 0) },
        { "Shoulder_R",  (BodyRegion.UpperBody, 0) },
        { "Elbow_L",     (BodyRegion.UpperBody, 0) },
        { "Elbow_R",     (BodyRegion.UpperBody, 0) },

        // Hands
        { "Hand_R",      (BodyRegion.Hands, 1) },
        { "Hand_L",      (BodyRegion.Hands, 0) },

        // Lower body
        { "UpperLeg_L",  (BodyRegion.LowerBody, 1) },
        { "UpperLeg_R",  (BodyRegion.LowerBody, 0) },
        { "LowerLeg_L",  (BodyRegion.LowerBody, 0) },
        { "LowerLeg_R",  (BodyRegion.LowerBody, 0) },

        // Feet
        { "Ball_L",      (BodyRegion.Feet, 1) },
        { "Ball_R",      (BodyRegion.Feet, 0) },
    };

    private static readonly Dictionary<BodyRegion, float> Weights = new()
    {
        { BodyRegion.Head,      1.0f },
        { BodyRegion.UpperBody, 1.0f },
        { BodyRegion.LowerBody, 0.8f },
        { BodyRegion.Feet,      0.6f },
        { BodyRegion.Hands,     0.4f },
    };

    // Serialized field names on PerceptionPoint. Renaming a field there means renaming it here,
    // or FindProperty returns null.
    private const string RegionField = "region";
    private const string WeightField = "weight";
    private const string PriorityField = "priority";

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

                if (!Bones.TryGetValue(collider.name, out var entry))
                {
                    Debug.LogWarning($"No region for collider '{collider.name}'.", collider);
                    continue;
                }

                found.Add(collider.name);

                PerceptionPoint point = collider.GetComponent<PerceptionPoint>();
                if (point == null)
                    point = collider.gameObject.AddComponent<PerceptionPoint>();

                var so = new SerializedObject(point);
                so.FindProperty(RegionField).enumValueIndex = (int)entry.region;
                so.FindProperty(WeightField).floatValue = Weights[entry.region];
                so.FindProperty(PriorityField).intValue = entry.priority;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // A renamed or deleted bone would otherwise leave its region without a cast point, silently.
        foreach (string bone in Bones.Keys)
        {
            if (!found.Contains(bone))
                Debug.LogWarning($"Bone '{bone}' in the table has no BoxCollider in {PlayerPrefabPath}.");
        }

        Debug.Log($"Mapped {found.Count} of {Bones.Count} perception points in {PlayerPrefabPath}.");
    }
}
