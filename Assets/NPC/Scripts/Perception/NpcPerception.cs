using System;
using UnityEngine;
using System.Collections.Generic;

// Answers "what can I see right now, and how well" once per tick, and accumulates nothing.
// Alarm is NpcSuspicion (M2); memory and f_duration are NpcWitness (M7). Checks run cheap to
// expensive and bail at the first failure: range, then flattened FOV angle, then one linecast 
// per body point in each attended region. Visibility is the weighted fraction of regions with 
// a clear line, which is the partial detection Phase 2 asks for. Quality factors stay separate 
// so the debug readout can print each one. Visibility is the weighted average of region fractions.
public class NpcPerception : MonoBehaviour, IPerceiver
{
    private static readonly BodyRegion[] Regions = (BodyRegion[])Enum.GetValues(typeof(BodyRegion));

    [SerializeField] private NpcArchetype archetype;
    [SerializeField] private LayerMask occluderMask;

    public event Action<Observation> Perceived;

    private PerceptionTargets targets;
    private PlayerStance playerStance;
    private float nextTick;

    private Observation latest;
    private bool hasLatest;
    private static int spawnIndex = 0;

    private bool[] pointClear;
    private float[] regionFraction;
    private int[] regionCount;
    private int[] regionClear;

    public NpcArchetype Archetype => archetype;
    public PerceptionTargets Targets => targets;
    public IReadOnlyList<bool> DebugPointClear => pointClear;
    public IReadOnlyList<float> DebugRegionFraction => regionFraction;
    public Vector3 EyePosition => transform.position + Vector3.up * archetype.EyeHeight;

    public bool TryGetLatest(out Observation observation)
    {
        observation = latest;
        return hasLatest;
    }

    private void Start()
    {
        if (archetype == null)
        {
            Debug.LogWarning($"{name} has no NpcArchetype assigned.", this);
            enabled = false;
            return;
        }

        targets = FindAnyObjectByType<PerceptionTargets>();
        if (targets == null)
        {
            Debug.LogWarning($"{name} found no PerceptionTargets in the scene.", this);
            enabled = false;
            return;
        }

        playerStance = targets.GetComponent<PlayerStance>();
        if (playerStance == null)
        {
            Debug.LogWarning($"{name} found no PlayerStance component", this);
            enabled = false;
            return;
        }

        pointClear = new bool[targets.AllPoints.Count];
        regionFraction = new float[Regions.Length];
        regionCount = new int[Regions.Length];
        regionClear = new int[Regions.Length];

        float interval = 1f / archetype.TickRate;
        float offset = (spawnIndex++ % 8) / 8f * interval;
        nextTick = Time.time + offset;
    }

    private void Update()
    {
        if (Time.time < nextTick)
            return;

        nextTick = Time.time + 1f / archetype.TickRate;

        Tick();
    }

    private void Tick()
    {
        hasLatest = TryPerceive(out latest);
        if (hasLatest)
        {
            Perceived?.Invoke(latest);
        }
    }

    private bool TryPerceive(out Observation observation)
    {
        observation = default;
        Array.Clear(pointClear, 0, pointClear.Length);
        Array.Clear(regionFraction, 0, regionFraction.Length);
        Array.Clear(regionCount, 0, regionCount.Length);
        Array.Clear(regionClear, 0, regionClear.Length);

        Vector3 toPlayer = targets.transform.position - EyePosition;

        if (toPlayer.sqrMagnitude > archetype.ViewDistance * archetype.ViewDistance)
            return false;

        Vector3 flatToPlayer = new Vector3(toPlayer.x, 0f, toPlayer.z);
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);

        float angle = Vector3.Angle(flatForward, flatToPlayer);

        if (angle > archetype.FovDegrees * 0.5f)
            return false;

        Vector3 eye = EyePosition;
        IReadOnlyList<PerceptionPoint> points = targets.AllPoints;

        for (int i = 0; i < points.Count; i++)
        {
            PerceptionPoint point = points[i];
            if (archetype.RegionWeight(point.Region) <= 0f)
                continue;

            int r = (int)point.Region;
            bool clear = !Physics.Linecast(eye, point.WorldPoint, occluderMask, QueryTriggerInteraction.Ignore);

            regionCount[r]++;
            if (clear)
            {
                regionClear[r]++;
                pointClear[i] = true;
            }
        }

        float total = 0f;
        float seen = 0f;

        for (int r = 0; r < Regions.Length; r++)
        {
            float weight = archetype.RegionWeight(Regions[r]);
            if (weight <= 0f || regionCount[r] == 0)
                continue;

            float fraction = regionClear[r] / (float)regionCount[r];
            regionFraction[r] = fraction;
            seen += weight * fraction;
            total += weight;
            observation.SetRegionVisible(Regions[r], regionClear[r] > 0);
        }

        float visibility = total > 0 ? seen / total : 0;
        if (visibility <= 0)
            return false;

        observation.fDistance = archetype.EvaluateDistance(toPlayer.magnitude / archetype.ViewDistance);
        observation.fAngle = archetype.EvaluateAngle(angle / (archetype.FovDegrees * 0.5f));
        observation.fLight = 1f;
        observation.fStance = playerStance.Current == Stance.Crouching ? archetype.CrouchVisibilityFactor : 1f;
        observation.perceiver = this;
        observation.timestamp = Time.time;
        observation.playerPosition = targets.transform.position;
        observation.visibility = visibility;
        return true;
    }
}
