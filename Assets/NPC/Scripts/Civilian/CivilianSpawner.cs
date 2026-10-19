using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Spawns a level's civilians once at load: each group gets a random count in its range, at random free
// points of its own, and takes the group's role. One prefab serves every role, so the role is applied
// right after Instantiate. Runs in Awake, like SecurityDirector, so the civilians' own Start still finds
// the player and every CivilianSpot has registered before the first Update picks one. Nothing changes
// during a run: no arrivals, no departures. Draws its spawn points as gizmos, since they are empty
// transforms, and warns about groups that cannot be filled or have too few CivilianSpots to go to
public class CivilianSpawner : MonoBehaviour
{
    private const float GizmoRadius = 0.3f;

    [SerializeField, Tooltip("Each group is rolled and spawned once at load")]
    private CivilianSpawnGroup[] groups;

    [Header("Debug")]
    [SerializeField, Tooltip("Repeats the whole roll when not 0. Reordering the groups changes it")]
    private int seed;

    private void Awake()
    {
        if (groups == null || groups.Length == 0)
        {
            Debug.LogWarning($"{name} has no civilian groups", this);
            enabled = false;
            return;
        }

        // One generator runs through every group in order, so a seed repeats the whole level's roll
        System.Random random = seed != 0 ? new System.Random(seed) : new System.Random();
        foreach (CivilianSpawnGroup group in groups)
            Spawn(group, random);
    }

    private void Spawn(CivilianSpawnGroup group, System.Random random)
    {
        if (group.prefab == null || group.points == null)
        {
            Debug.LogWarning($"{name}: a {group.role} group has no prefab or no points", this);
            return;
        }

        // Copied so each point is used once; empty slots are skipped
        List<Transform> free = new List<Transform>();
        foreach (Transform point in group.points)
            if (point != null) free.Add(point);

        // Next's upper bound is exclusive, and it throws when the minimum is above the maximum
        int low = Mathf.Max(0, group.count.x);
        int high = Mathf.Max(low, group.count.y);
        int count = Mathf.Min(random.Next(low, high + 1), free.Count);

        for (int i = 0; i < count; i++)
        {
            int pick = random.Next(free.Count);
            Transform point = free[pick];
            free.RemoveAt(pick);

            CivilianBehaviour civilian = Instantiate(group.prefab, point.position, point.rotation);
            civilian.AssignRole(group.role);
            AssignArchetype(civilian, group);
            civilian.name = $"{group.role} {i}";
        }

        Debug.Log($"Civilians: {count} {group.role} spawned (range {low} to {high})");
    }

    private void AssignArchetype(CivilianBehaviour civilian, CivilianSpawnGroup group)
    {
        if (group.archetype == null) return;

        NpcPerception perception = civilian.GetComponent<NpcPerception>();
        if (perception == null)
        {
            Debug.LogWarning($"{name}: {civilian.name} has no NpcPerception, so the {group.role} archetype was not applied", this);
            return;
        }
        perception.AssignArchetype(group.archetype);
    }

    // In Start, not Awake: every CivilianSpot adds itself to the registry in OnEnable
    private void Start()
    {
        int roles = System.Enum.GetValues(typeof(CivilianRole)).Length;
        int[] needed = new int[roles];
        int[] available = new int[roles];

        // Groups of one role share its spots, so their maximums add up
        foreach (CivilianSpawnGroup group in groups)
            needed[(int)group.role] += Mathf.Max(0, group.count.y);
        foreach (CivilianSpot spot in CivilianSpot.All)
            available[(int)spot.Role]++;

        for (int role = 0; role < roles; role++)
            if (needed[role] > available[role])
                Debug.LogWarning($"{name}: up to {needed[role]} {(CivilianRole)role} can spawn but only {available[role]} {(CivilianRole)role} CivilianSpots exist; the rest will wait for a free one", this);
    }

    private void OnValidate()
    {
        if (groups == null) return;

        for (int i = 0; i < groups.Length; i++)
        {
            CivilianSpawnGroup group = groups[i];
            if (group.prefab == null)
                Debug.LogWarning($"{name}: group {i} ({group.role}) has no prefab", this);
            if (group.count.x > group.count.y)
                Debug.LogWarning($"{name}: group {i} ({group.role}) count minimum is above its maximum", this);

            int points = 0;
            if (group.points != null)
                foreach (Transform point in group.points)
                    if (point != null) points++;

            if (points == 0)
                Debug.LogWarning($"{name}: group {i} ({group.role}) has no spawn points", this);
            else if (group.count.y > points)
                Debug.LogWarning($"{name}: group {i} ({group.role}) can spawn up to {group.count.y} but has {points} points, so the count is capped", this);
        }
    }

    // The points are empty transforms, so without this they are invisible in the Scene view
    private void OnDrawGizmos()
    {
        if (groups == null) return;

        for (int i = 0; i < groups.Length; i++)
        {
            CivilianSpawnGroup group = groups[i];
            if (group.points == null) continue;

            Gizmos.color = GroupColor(group.role);
            for (int p = 0; p < group.points.Length; p++)
            {
                Transform point = group.points[p];
                if (point == null) continue;

                Gizmos.DrawWireSphere(point.position, GizmoRadius);
                Gizmos.DrawRay(point.position, point.forward);
#if UNITY_EDITOR
                Handles.Label(point.position + Vector3.up * 0.5f, $"{group.role} {group.count.x}-{group.count.y}  point {p}");
#endif
            }
        }
    }

    // Same colours as CivilianSpot, so a role reads the same on its spawn points and its spots
    private static Color GroupColor(CivilianRole role) => role switch
    {
        CivilianRole.Customer => new Color(0.3f, 0.9f, 0.3f),
        CivilianRole.Employee => new Color(0.3f, 0.6f, 1f),
        _ => new Color(0.7f, 0.4f, 1f),
    };
}
