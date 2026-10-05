using System.Collections.Generic;
using UnityEngine;

// Light level at the player, as NPC perception reads it. Samples every GameplayLight on a tick,
// with a shadow ray per lit light, and caches the result so any number of NPCs can read it. Light
// is the player's property, not the observer's, so it is computed once here rather than per NPC.
public class PlayerLighting : MonoBehaviour
{
    [Header("Sampling")]
    [SerializeField, Tooltip("Where light is measured. Usually the chest bone")]
    private Transform samplePoint;

    [SerializeField, Tooltip("What blocks light. Same layer as perception rays: Occluder")]
    private LayerMask occluderMask;

    [SerializeField, Min(1f), Tooltip("Samples per second")]
    private float tickRate = 10f;

    [Header("Response")]
    [SerializeField, Range(0f, 1f), Tooltip("Level when no gameplay light reaches the player. Stands in for ambient and bounce light")]
    private float ambientLevel = 0.1f;

    [SerializeField, Min(0.01f), Tooltip("Raw light sum that counts as full light. Tune under the brightest lamp")]
    private float fullLightIntensity = 1f;

    private float nextTick;

    public float Level { get; private set; } = 1f;
    public float RawIntensity { get; private set; }
    public int DebugLitCount { get; private set; }
    public int DebugShadowedCount { get; private set; }
    public Transform SamplePoint => samplePoint;

    private void Awake()
    {
        if (samplePoint == null)
        {
            Debug.LogWarning($"{name} has no sample point; light reads as full.", this);
            enabled = false;
        }
    }

    private void Start() => Sample();
    private void Update()
    {
        if (Time.time < nextTick) return;
        nextTick = Time.time + 1f / tickRate;
        Sample();
    }

    private void Sample()
    {
        Vector3 point = samplePoint.position;
        IReadOnlyList<GameplayLight> lights = GameplayLight.All;
        float raw = 0f;
        int shadowed = 0;
        int lit = 0;

        for (int i = 0; i < lights.Count; i++)
        {
            float contribution = lights[i].Contribution(point);
            if (contribution <= 0f) continue;
            if (IsShadowed(lights[i], point))
            {
                shadowed++;
                continue;
            }
            raw += contribution;
            lit++;
        }

        RawIntensity = raw;
        DebugLitCount = lit;
        DebugShadowedCount = shadowed;
        Level = Mathf.Max(ambientLevel, Mathf.Clamp01(raw / fullLightIntensity));
    }

    private bool IsShadowed(GameplayLight light, Vector3 point)
    {
        if (light.Source.shadows == LightShadows.None) return false;
        return Physics.Linecast(light.transform.position, point, occluderMask, QueryTriggerInteraction.Ignore);
    }

    private void OnDrawGizmosSelected()
    {
        if (samplePoint == null) return;
        Vector3 point = samplePoint.position;
        IReadOnlyList<GameplayLight> lights = GameplayLight.All;

        for (int i = 0; i < lights.Count; i++)
        {
            if (lights[i].Contribution(point) <= 0f) continue;
            Gizmos.color = IsShadowed(lights[i], point) ? Color.red : Color.green;
            Gizmos.DrawLine(point, lights[i].transform.position);
        }
    }
}
