using System;
using UnityEngine;

// How alarmed this NPC is: alarm, not memory (decision 7). Rises with strength × abnormality while
// something abnormal is in view; decays after a delay otherwise, including while the player is seen
// behaving normally, so being seen alone never raises it (decision 6). State changes use hysteresis
// so an NPC hovering at a threshold does not flicker.
// M8: a guard posted at a SecurityMonitor will also need to take observations from cameras, not
// only from its own perception. Today suspicion reads only this NPC's own eyes.
[RequireComponent(typeof(NpcPerception))]
public class NpcSuspicion : MonoBehaviour
{
    private NpcPerception perception;
    private NpcHearing hearing;
    private NpcAnomalySight anomalySight;

    public float Suspicion { get; private set; }
    public NpcAlertState State { get; private set; }

    // From and to. A spike can skip states (Idle to Investigating in one frame), so consumers must
    // not assume single steps.
    public event Action<NpcAlertState, NpcAlertState> AlertStateChanged;

    // Skill hooks (concept 7.4): Ghost slows the rise in shadow, Face speeds the decay. Set by other
    // systems at runtime and never written into the archetype. Clamped at zero, since a negative
    // multiplier would turn rise into decay.
    private float riseMultiplier = 1f;
    public float RiseMultiplier
    {
        get => riseMultiplier;
        set => riseMultiplier = Mathf.Max(0f, value);
    }

    private float decayMultiplier = 1f;
    public float DecayMultiplier
    {
        get => decayMultiplier;
        set => decayMultiplier = Mathf.Max(0f, value);
    }

    // Suspicion never decays below this for the rest of the job. 0 unless a role raises it: a guard who
    // found the safe open stays on edge (thrift shop G3). Only ever goes up
    public float Baseline { get; private set; }

    // Where the NPC's attention last went: every sighting, and alarming sounds while the player is out
    // of sight. Ordinary footsteps never move it, or an alert NPC would track a quiet player through
    // walls step by step. The indicator's arrow points here, and it is the same rule GuardBehaviour
    // uses for its last-known position, so the two always agree
    public Vector3 LastStimulus { get; private set; }
    public bool HasStimulus { get; private set; }

    private float lastAbnormalTime = float.NegativeInfinity;

    private void Awake()
    {
        perception = GetComponent<NpcPerception>();
        hearing = GetComponent<NpcHearing>();
        anomalySight = GetComponent<NpcAnomalySight>();
    }

    private void OnEnable()
    {
        if (hearing != null)
            hearing.Heard += OnHeard;

        if (anomalySight != null)
            anomalySight.Noticed += OnNoticed;
    }

    private void OnDisable()
    {
        if (hearing != null)
            hearing.Heard -= OnHeard;

        if (anomalySight != null)
            anomalySight.Noticed -= OnNoticed;
    }

    private void Start()
    {
        if (perception.Archetype == null)
        {
            Debug.LogWarning($"{name} has no NpcArchetype; suspicion disabled.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        NpcArchetype archetype = perception.Archetype;
        float suspicion = Suspicion;

        bool sees = perception.TryGetLatest(out Observation observation);
        if (sees)
        {
            LastStimulus = observation.playerPosition;
            HasStimulus = true;
        }

        // Recognition alone stops just short of Alerted (thrift shop §6): a known outfit walking normally gets a
        // closer look; only conduct on top tips the NPC into pursuit
        float recognitionCeiling = archetype.Threshold(NpcAlertState.Alerted) - archetype.Hysteresis;
        bool abnormal = sees && observation.abnormality > 0f;
        bool recognised = sees && observation.recognition > 0f && suspicion < recognitionCeiling;

        if (abnormal || recognised)
        {
            float weight = 1f - (1f - observation.abnormality) * (1f - observation.recognition);   // decision 6
            suspicion += observation.Strength * weight * archetype.RiseRate * RiseMultiplier * Time.deltaTime;
            if (!abnormal) suspicion = Mathf.Min(suspicion, recognitionCeiling);
            lastAbnormalTime = Time.time;
        }
        else if (Time.time - lastAbnormalTime >= archetype.DecayDelay)
            suspicion -= archetype.DecayRate * DecayMultiplier * Time.deltaTime;

        Suspicion = Mathf.Clamp(suspicion, Baseline, 1f);
        UpdateState();
    }

    // A lower value never undoes a higher one, so calling it every tick is harmless
    public void RaiseBaseline(float value)
    {
        Baseline = Mathf.Max(Baseline, Mathf.Clamp01(value));
        if (Suspicion >= Baseline) return;

        Suspicion = Baseline;
        UpdateState();
    }

    private void UpdateState()
    {
        NpcArchetype archetype = perception.Archetype;
        NpcAlertState target = State;

        while (target < NpcAlertState.Alerted && Suspicion >= archetype.Threshold(target + 1))
            target++;

        while (target > NpcAlertState.Idle && Suspicion < archetype.Threshold(target) - archetype.Hysteresis)
            target--;

        if (target == State) return;

        NpcAlertState from = State;
        State = target;
        AlertStateChanged?.Invoke(from, target);
    }

    // A sound can raise suspicion up to alarm × loudness × the archetype's hearing weight, never past it:
    // the loudest thing heard sets how worried the NPC is, and repeated footsteps don't stack. It also
    // resets the decay delay, so a heard sprint holds suspicion like a seen one.
    private void OnHeard(HeardNoise noise)
    {
        // Attention first: a sound the NPC turns toward is a stimulus even if it raises nothing
        bool seesPlayer = perception.TryGetLatest(out _);
        bool matters = noise.Alarm > 0f;
        if (matters && !seesPlayer)
        {
            LastStimulus = noise.Position;
            HasStimulus = true;
        }

        if (noise.Alarm <= 0f) return;

        float floor = noise.Alarm * noise.Loudness * perception.Archetype.HearingSuspicion * RiseMultiplier;
        if (floor <= Suspicion) return;

        Suspicion = floor;
        lastAbnormalTime = Time.time;
        UpdateState();
    }

    // An anomaly sets a floor like a sound does, so seeing one for many ticks doesn't stack. Noticed
    // repeats every tick it is in view, which keeps resetting the decay delay: an open door in view
    // keeps the NPC on edge until someone deals with it
    private void OnNoticed(Anomaly anomaly)
    {
        // Weight 0: noticed but not suspicious. No stimulus and no decay reset, or a customer near an
        // open door would stay on edge forever; what the NPC does about it is its role's business
        float weight = perception.Archetype.AnomalyWeight(anomaly.Kind);
        if (weight <= 0f) return;

        if (!perception.TryGetLatest(out _))
        {
            LastStimulus = anomaly.SightPoint;
            HasStimulus = true;
        }

        float floor = perception.Archetype.AnomalySuspicion * weight * riseMultiplier;
        lastAbnormalTime = Time.time;
        if (floor <= Suspicion) return;

        Suspicion = floor;
        UpdateState();
    }
}
