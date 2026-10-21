using System;
using UnityEngine;
using System.Collections.Generic;

// Answers "what can I see right now, and how well" once per tick, and accumulates nothing.
// Alarm is NpcSuspicion, memory and f_duration are NpcWitness (M7). Checks run cheap to
// expensive and bail at the first failure: range, then flattened FOV angle, then one linecast 
// per body point in each attended region. Visibility is the weighted average of region fractions,
// which is the partial detection Phase 2 asks for. Quality factors stay separate so the debug
// readout can print each one. Conduct is read at tick time and judged by this NPC's archetype.
// Light is read from PlayerLighting, not sampled here: it is the player's property, computed once.
// InViewCone is the one cone test every sight check shares (player, anomalies, later cameras).
public class NpcPerception : MonoBehaviour, IPerceiver
{
    private static readonly BodyRegion[] Regions = (BodyRegion[])Enum.GetValues(typeof(BodyRegion));

    [SerializeField] private NpcArchetype archetype;
    [SerializeField] private LayerMask occluderMask;

    public event Action<Observation> Perceived;

    private PerceptionTargets targets;
    private PlayerStance playerStance;
    private PlayerConduct playerConduct;
    private PlayerLighting playerLighting;
    private PlayerAppearance playerAppearance;
    private float nextTick;

    private Observation latest;
    private bool hasLatest;
    private float rangeScale = 1f;
    private float fovScale = 1f;
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
    public float RangeScale => rangeScale;
    public float FovScale => fovScale;
    public float ViewDistance => archetype.ViewDistance * rangeScale;
    public float FovDegrees => archetype.FovDegrees * fovScale;

    public void SetViewScale(float range, float fov)
    {
        rangeScale = Mathf.Clamp01(range);
        fovScale = Mathf.Clamp01(fov);
    }

    // For a spawner that builds different kinds of NPC from one prefab: call it right after Instantiate,
    // before Start. Nothing caches the archetype (every reader asks this component each time), and Start
    // only takes its tick rate, so a swap this early is complete
    public void AssignArchetype(NpcArchetype value)
    {
        if (value != null) archetype = value;
    }

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

        playerConduct = targets.GetComponent<PlayerConduct>();
        if (playerConduct == null)
            Debug.LogWarning($"{name} found no PlayerConduct; conduct reads as None.", this);

        playerLighting = targets.GetComponent<PlayerLighting>();
        if (playerLighting == null)
            Debug.LogWarning($"{name} found no PlayerLighting; light reads as full.", this);

        playerAppearance = targets.GetComponent<PlayerAppearance>();
        if (playerAppearance == null)
            Debug.LogWarning($"{name} found no PlayerAppearance; witnesses will learn nothing about the outfit.", this);

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

    // Range, then the flattened FOV angle: the cone test every sight check shares. Flattened because
    // targets sit low (the player's root is at the feet) and the eye sits high
    public bool InViewCone(Vector3 point, out float angle)
    {
        angle = 0f;
        Vector3 toPoint = point - EyePosition;
        if (toPoint.sqrMagnitude > ViewDistance * ViewDistance) return false;

        Vector3 flatToPoint = new Vector3(toPoint.x, 0f, toPoint.z);
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);
        angle = Vector3.Angle(flatForward, flatToPoint);
        return angle <= FovDegrees * 0.5f;
    }

    private bool TryPerceive(out Observation observation)
    {
        observation = default;
        Array.Clear(pointClear, 0, pointClear.Length);
        Array.Clear(regionFraction, 0, regionFraction.Length);
        Array.Clear(regionCount, 0, regionCount.Length);
        Array.Clear(regionClear, 0, regionClear.Length);

        Vector3 toPlayer = targets.transform.position - EyePosition;

        if (!InViewCone(targets.transform.position, out float angle))
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
            observation.SetRegionFraction(Regions[r], fraction);
        }

        float visibility = total > 0 ? seen / total : 0;
        if (visibility <= 0)
            return false;

        observation.fDistance = archetype.EvaluateDistance(toPlayer.magnitude / ViewDistance);
        observation.fAngle = archetype.EvaluateAngle(angle / (FovDegrees * 0.5f));
        observation.fLight = archetype.LightFactor(playerLighting != null ? playerLighting.Level : 1f);
        observation.fStance = playerStance.Current == Stance.Crouching ? archetype.CrouchVisibilityFactor : 1f;
        observation.perceiver = this;
        observation.timestamp = Time.time;
        observation.playerPosition = targets.transform.position;
        observation.visibility = visibility;
        observation.conduct = playerConduct != null ? playerConduct.Current : ConductFlags.None;
        observation.abnormality = archetype.Abnormality(observation.conduct);
        observation.appearance = playerAppearance != null ? playerAppearance.Current : default;
        // Observers who recognise no one (every civilian) skip the match
        observation.recognition = archetype.Recognition > 0f
            ? archetype.Recognition * JobWitnesses.RecordMatch(observation.appearance)
            : 0f;
        return true;
    }
}
