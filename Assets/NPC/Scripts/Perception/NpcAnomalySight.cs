using System;
using System.Collections.Generic;
using UnityEngine;

// Finds the raised anomalies this NPC can see, on its perception tick (decision 18): the shared cone
// test, then one linecast to each anomaly's sight point. Stateless like NpcPerception: Noticed fires
// on every tick an anomaly is in view, and consumers decide what it means (suspicion, an errand).
// Only raised anomalies are registered, so while nothing is wrong this costs nothing.
[RequireComponent(typeof(NpcPerception))]
public class NpcAnomalySight : MonoBehaviour
{
    [SerializeField, Tooltip("What blocks sight. Same layer as perception rays: Occluder")]
    private LayerMask occluderMask;

    public event Action<Anomaly> Noticed;

    private static int spawnIndex;
    private NpcPerception perception;
    private float nextTick;

#if UNITY_EDITOR
    public Anomaly DebugLastNoticed { get; private set; }
    public float DebugTime { get; private set; } = float.NegativeInfinity;
#endif

    private void Awake() => perception = GetComponent<NpcPerception>();

    private void Start()
    {
        if (perception.Archetype == null)
        {
            enabled = false;
            return;
        }

        float interval = 1f / perception.Archetype.TickRate;
        float offset = (spawnIndex++ % 8) / 8f * interval;
        nextTick = Time.time + offset;
    }

    private void Update()
    {
        if (Time.time < nextTick)
            return;

        nextTick = Time.time + 1f / perception.Archetype.TickRate;

        Tick();
    }

    private void Tick()
    {
        IReadOnlyList<Anomaly> raised = Anomaly.Raised;
        if (raised.Count == 0) return;

        Vector3 eye = perception.EyePosition;

        // Every raised anomaly is checked, whatever its weight: weight 0 means "not suspicious", not
        // "not noticed" (a civilian sees an open door without suspicion, decision 18)
        for (int i = raised.Count - 1; i >= 0; i--)
        {
            Anomaly anomaly = raised[i];
            Vector3 point = anomaly.SightPoint;
            if (!perception.InViewCone(point, out _)) continue;
            if (Physics.Linecast(eye, point, occluderMask, QueryTriggerInteraction.Ignore)) continue;

#if UNITY_EDITOR
            DebugLastNoticed = anomaly;
            DebugTime = Time.time;
#endif

            Noticed?.Invoke(anomaly);
        }
    }
}
