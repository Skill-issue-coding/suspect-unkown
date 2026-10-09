using System;
using System.Collections.Generic;
using UnityEngine;

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

        NpcArchetype archetype = perception.Archetype;
        Vector3 eye = perception.EyePosition;

        for (int i = raised.Count - 1; i >= 0; i--)
        {
            Anomaly anomaly = raised[i];
            if (archetype.AnomalyWeight(anomaly.Kind) <= 0f) continue;
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
