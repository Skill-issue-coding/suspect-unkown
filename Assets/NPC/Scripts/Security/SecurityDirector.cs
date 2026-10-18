using System.Collections.Generic;
using UnityEngine;

// Applies adaptive security when the level loads (thriftshop-level.md 5.3, decision 24): loads the saved
// ActionRecord, builds the plan from it and the level's SecurityTier, switches on the chosen candidate
// POIs with their drain multipliers, and spawns the guards at random free spawns. Runs once in Awake and
// never changes anything during a run, so the plan is fixed for the whole heist. The pure work lives in
// ActionModel and SecurityPlanner; this class only connects them to the scene
[RequireComponent(typeof(SecurityLayout))]
public class SecurityDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("The level's rooms and entrances")]
    private JobLayout jobLayout;

    [SerializeField, Tooltip("Guard spawned for each guard in the plan")]
    private GuardBehaviour guardPrefab;

    [Header("Model")]
    [SerializeField, Min(0.01f), Tooltip("Laplace smoothing for the action model")]
    private float alpha = 0.5f;

    [SerializeField, Min(1), Tooltip("Forward steps the occupancy is propagated")]
    private int steps = 6;

    [SerializeField, Tooltip("A candidate's detection chance, 0 to 1")]
    private float detection = 0.6f;

    [Header("Debug")]
    [SerializeField, Tooltip("Repeats the guard spawn roll when not 0")]
    private int spawnSeed;

    private ActionRecord record;
    private ActionModel model;
    private PoiCandidate[] candidates;
    private SecurityPlan plan;

    private SecurityLayout securityLayout;

    public ActionRecord Record => record;
    public ActionModel Model => model;
    public IReadOnlyList<PoiCandidate> Candidates => candidates;
    public SecurityPlan Plan => plan;

    private void Awake()
    {
        securityLayout = GetComponent<SecurityLayout>();
        if (jobLayout == null || guardPrefab == null)
        {
            Debug.LogWarning($"{name} needs a JobLayout and a guard prefab", this);
            enabled = false;
            return;
        }

        record = LoadRecord();
        model = new ActionModel(record, EntranceRooms(), alpha, steps);
        candidates = BuildCandidates();
        SecurityTier tier = securityLayout.TierFor(record.completedRuns);
        plan = SecurityPlanner.Plan(record, model, candidates, detection, tier);

        ActivatePicks(plan);
        SpawnGuards(plan);
        LogPlan(record, plan);
    }

    private ActionRecord LoadRecord()
    {
        string location = gameObject.scene.name;
        record = ActionStore.Load().Find(location);
        if (record == null) return ActionRecord.For(location, jobLayout);
        if (!record.Fits(jobLayout))
        {
            Debug.LogWarning($"{name}: the saved record for {location} no longer fits the layout, planning as a first run", this);
            return ActionRecord.For(location, jobLayout);
        }
        return record;
    }
    private int[] EntranceRooms()
    {
        int[] rooms = new int[jobLayout.EntranceCount];
        for (int i = 0; i < rooms.Length; i++)
            rooms[i] = jobLayout.EntranceRoom(i);
        return rooms;
    }

    private PoiCandidate[] BuildCandidates()
    {
        SecurityCandidate[] authored = securityLayout.Candidates;
        PoiCandidate[] result = new PoiCandidate[authored.Length];
        for (int i = 0; i < authored.Length; i++)
        {
            SecurityCandidate candidate = authored[i];
            string candidateName = candidate.poi != null ? candidate.poi.name : $"Candidate {i}";
            result[i] = new PoiCandidate(candidateName, RoomIndices(candidate.rooms, candidateName), EntranceIndices(candidate.entrances, candidateName));
        }
        return result;
    }

    private int[] RoomIndices(Zone[] zones, string owner)
    {
        List<int> indices = new List<int>();
        if (zones != null)
            foreach (Zone zone in zones)
            {
                int room = jobLayout.RoomIndex(zone);
                if (room < 0) Debug.LogWarning($"{name}: {owner} lists a room that is not in the JobLayout", this);
                else indices.Add(room);
            }
        return indices.ToArray();
    }

    private int[] EntranceIndices(Entrance[] entrances, string owner)
    {
        List<int> indices = new List<int>();
        if (entrances != null)
            foreach (Entrance entrance in entrances)
            {
                int index = jobLayout.EntranceIndex(entrance);
                if (index < 0) Debug.LogWarning($"{name}: {owner} lists an entrance that is not in the JobLayout", this);
                else indices.Add(index);
            }
        return indices.ToArray();
    }

    private void LogPlan(ActionRecord record, SecurityPlan plan)
    {
        Debug.Log($"Security: {record.completedRuns} completed runs, {plan.Guards} guards, {plan.Picks.Count} adaptive POIs");
        foreach (SecurityPick pick in plan.Picks)
            Debug.Log($"Security: {pick.explanation}");
    }

    private void ActivatePicks(SecurityPlan plan)
    {
        SecurityCandidate[] authored = securityLayout.Candidates;
        foreach (SecurityPick pick in plan.Picks)
        {
            PointOfInterest poi = authored[pick.candidate].poi;
            if (poi == null)
            {
                Debug.LogWarning($"{name}: candidate {pick.candidate} has no POI to activate", this);
                continue;
            }

            poi.DecayMultiplier = pick.decayMultiplier;
            poi.gameObject.SetActive(true);
            Debug.Log($"Security: {poi.name} on, importance {poi.Importance:0.00}");
        }
    }

    private void SpawnGuards(SecurityPlan plan)
    {
        // Copy of the spawns so each is used once; skip empty slots
        List<Transform> free = new List<Transform>();
        foreach (Transform spawn in securityLayout.GuardSpawns)
            if (spawn != null) free.Add(spawn);

        System.Random random = spawnSeed != 0 ? new System.Random(spawnSeed) : new System.Random();

        for (int i = 0; i < plan.Guards; i++)
        {
            if (free.Count == 0)
            {
                Debug.LogWarning($"{name}: out of spawns after {i} of {plan.Guards} guards", this);
                return;
            }

            int pick = random.Next(free.Count);
            Transform spawn = free[pick];
            free.RemoveAt(pick);

            GuardBehaviour guard = Instantiate(guardPrefab, spawn.position, spawn.rotation);
            guard.name = $"Guard {i}";
            Debug.Log($"Security: {guard.name} spawned at {spawn.name}");
        }
    }
}
