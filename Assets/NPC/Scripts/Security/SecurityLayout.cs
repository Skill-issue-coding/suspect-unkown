using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// SecurityLayout holds the level's guard spawns, candidate POIs and a reference to its security budget
// asset. It is read alongside JobLayout. The tiers live in the asset, not here, so the main menu can
// read the same numbers without loading the level
public class SecurityLayout : MonoBehaviour
{
    private static readonly Color spawnColor = Color.cyan;
    private static readonly Color candidateColor = new Color(1f, 0.6f, 0f);

    [Header("Guards")]
    [SerializeField, Tooltip("Spawn points inside shop, on NavMesh")]
    private Transform[] guardSpawns;

    [Header("Adaptive POIs")]
    [SerializeField, Tooltip("Index order; ties go to lower index")]
    private SecurityCandidate[] candidates;

    [Header("Budget")]
    [SerializeField, Tooltip("This level's guards and adaptive POIs per completed-run count. Shared with the main menu")]
    private SecurityBudget budget;

    // A missing budget gives the zero tier, so the director spawns no guards; OnValidate warns about it
    public SecurityTier TierFor(int completedRuns) => budget != null ? budget.TierFor(completedRuns) : default;

    public Transform[] GuardSpawns => guardSpawns;
    public SecurityCandidate[] Candidates => candidates;

    private void OnValidate()
    {
        if (guardSpawns == null || guardSpawns.Length == 0)
            Debug.LogWarning($"{name}: no guard spawns, the director cannot place guards", this);

        ValidateTiers();

        if (candidates == null) return;
        for (int i = 0; i < candidates.Length; i++)
        {
            SecurityCandidate candidate = candidates[i];
            if (candidate.poi == null)
            {
                Debug.LogWarning($"{name}: candidate {i} has no POI", this);
                continue;
            }
            if (candidate.entrances != null && candidate.entrances.Length > 1)
                Debug.LogWarning($"{name}: candidate {i} ({candidate.poi.name}) covers more than one entrance", this);
            if (candidate.poi.gameObject.activeSelf)
                Debug.LogWarning($"{name}: candidate {i} ({candidate.poi.name}) is active; it would run as a base POI every run", this);
        }
    }

    // Only what needs this scene: the budget asset checks its own tiers (SecurityBudget.OnValidate)
    private void ValidateTiers()
    {
        if (budget == null)
        {
            Debug.LogWarning($"{name}: no security budget assigned, the director has no budget", this);
            return;
        }

        int spawns = guardSpawns != null ? guardSpawns.Length : 0;
        int candidateCount = candidates != null ? candidates.Length : 0;

        for (int i = 0; i < budget.Tiers.Count; i++)
        {
            SecurityTier tier = budget.Tiers[i];
            if (tier.guards > spawns)
                Debug.LogWarning($"{name}: tier {i} wants {tier.guards} guards but there are {spawns} spawns", this);
            if (tier.adaptivePois > candidateCount)
                Debug.LogWarning($"{name}: tier {i} wants {tier.adaptivePois} adaptive POIs but there are {candidateCount} candidates", this);
        }
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        DrawSpawns();
        DrawCandidates();
#endif
    }

    private void DrawSpawns()
    {
        if (guardSpawns == null) return;
        Gizmos.color = spawnColor;
        for (int i = 0; i < guardSpawns.Length; i++)
        {
            Transform spawn = guardSpawns[i];
            if (spawn == null) continue;
            Gizmos.DrawWireSphere(spawn.position, 0.3f);
            Gizmos.DrawRay(spawn.position, spawn.forward);
#if UNITY_EDITOR
            Handles.Label(spawn.position + Vector3.up * 0.5f, $"Spawn {i}");
#endif
        }
    }

    private void DrawCandidates()
    {
        if (candidates == null) return;
        for (int i = 0; i < candidates.Length; i++)
        {
            SecurityCandidate candidate = candidates[i];
            if (candidate.poi == null) continue;

            Vector3 position = candidate.poi.transform.position;
            bool broken = candidate.entrances != null && candidate.entrances.Length > 1;
            Gizmos.color = broken ? Color.red : candidateColor;
            Gizmos.DrawWireSphere(position, 0.4f);

            if (candidate.entrances != null)
                foreach (Entrance entrance in candidate.entrances)
                    if (entrance != null) Gizmos.DrawLine(position, entrance.transform.position);

#if UNITY_EDITOR
            Handles.Label(position + Vector3.up * 0.7f,
                $"#{i} {candidate.poi.name}\nRooms: {Names(candidate.rooms)}\nEntrances: {Names(candidate.entrances)}");
#endif
        }
    }

#if UNITY_EDITOR
    private static string Names<T>(T[] list) where T : UnityEngine.Object
    {
        if (list == null || list.Length == 0) return "-";
        string[] names = new string[list.Length];
        for (int i = 0; i < list.Length; i++)
            names[i] = list[i] != null ? list[i].name : "(empty)";
        return string.Join(", ", names);
    }
#endif
}
