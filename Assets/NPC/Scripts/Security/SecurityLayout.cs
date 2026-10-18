using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// SecurityLayout holds the level's guard spawns and candidate POIs.
// It is read alongside JobLayout
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
    [SerializeField, Tooltip("Guards and adaptive POIs per completed-run count; the tier from 0 is the first run")]
    private SecurityTier[] tiers;

    public SecurityTier TierFor(int completedRuns) => SecurityTier.For(tiers, completedRuns);

    public Transform[] GuardSpawns => guardSpawns;
    public SecurityCandidate[] Candidates => candidates;

    private void OnValidate()
    {
        if (guardSpawns == null || guardSpawns.Length == 0)
            Debug.LogWarning($"{name}: no guard spawns, the director cannot place guards", this);

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

        ValidateTiers();
    }

    private void ValidateTiers()
    {
        if (tiers == null || tiers.Length == 0)
        {
            Debug.LogWarning($"{name}: no tiers, the director has no budget", this);
            return;
        }

        int spawns = guardSpawns != null ? guardSpawns.Length : 0;
        int candidateCount = candidates != null ? candidates.Length : 0;
        bool hasFirstRun = false;

        for (int i = 0; i < tiers.Length; i++)
        {
            SecurityTier tier = tiers[i];
            if (tier.fromCompletedRuns == 0) hasFirstRun = true;
            if (tier.guards < 1)
                Debug.LogWarning($"{name}: tier {i} has no guards", this);
            if (tier.guards > spawns)
                Debug.LogWarning($"{name}: tier {i} wants {tier.guards} guards but there are {spawns} spawns", this);
            if (tier.adaptivePois > candidateCount)
                Debug.LogWarning($"{name}: tier {i} wants {tier.adaptivePois} adaptive POIs but there are {candidateCount} candidates", this);
            if (i > 0 && tier.fromCompletedRuns <= tiers[i - 1].fromCompletedRuns)
                Debug.LogWarning($"{name}: tier {i} is not after tier {i - 1}; keep tiers sorted by completed runs", this);
            if (i > 0 && tier.guards < tiers[i - 1].guards)
                Debug.LogWarning($"{name}: tier {i} has fewer guards than tier {i - 1}; the guard count never shrinks", this);
        }

        if (!hasFirstRun)
            Debug.LogWarning($"{name}: no tier starts at 0 completed runs, a first run has no budget", this);
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
