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

    private float lastAbnormalTime = float.NegativeInfinity;

    private void Awake() => perception = GetComponent<NpcPerception>();

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

        if (perception.TryGetLatest(out Observation observation) && observation.abnormality > 0f)
        {
            suspicion += observation.Strength * observation.abnormality *
                archetype.RiseRate * RiseMultiplier * Time.deltaTime;

            lastAbnormalTime = Time.time;
        }
        else if (Time.time - lastAbnormalTime >= archetype.DecayDelay)
            suspicion -= archetype.DecayRate * DecayMultiplier * Time.deltaTime;

        Suspicion = Mathf.Clamp01(suspicion);
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
}
