using UnityEngine;
using System;
using System.Collections.Generic;

// Draws what NpcPerception already found; never casts a ray of its own, so what you tune is what
// the NPC used. Cone and rays show when the NPC is selected. Ray colours update at the perception
// tick rate while their endpoints follow the player every frame, so a lag of one tick is expected.
// Grey rays are points in regions the archetype ignores; no ray was cast.
[RequireComponent(typeof(NpcPerception))]
public class NpcPerceptionDebug : MonoBehaviour
{
    private static readonly BodyRegion[] Regions = (BodyRegion[])Enum.GetValues(typeof(BodyRegion));

    [Header("Cone")]
    [SerializeField] private bool gizmos = true;
    [SerializeField, Range(0.01f, 0.5f)] private float eyeGizmoRadius = 0.1f;
    [SerializeField] private Color gizmosColor = new Color(0.4f, 0.8f, 1.0f);

    [Header("Rays")]
    [SerializeField] private bool rays = true;
    [SerializeField] private Color clearRayColor = Color.green;
    [SerializeField] private Color blockedRayColor = Color.red;
    [SerializeField] private Color ignoredRayColor = Color.gray;

    [Header("Readout")]
    [SerializeField] private bool readouts = true;
    [SerializeField] private Rect readoutRect = new Rect(10f, 10f, 220f, 365f);

    private NpcPerception perception;
    private NpcSuspicion suspicion;

    private void OnDrawGizmosSelected()
    {
        if (!TryGetSuspicionAndPerception()) return;

        DrawCone();
        DrawRays();
    }

    private void DrawCone()
    {
        if (!gizmos) return;

        NpcArchetype archetype = perception.Archetype;

        Gizmos.color = gizmosColor;
        Gizmos.DrawWireSphere(perception.EyePosition, eyeGizmoRadius);

        Vector3 origin = transform.position;
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        float halfFov = archetype.FovDegrees * 0.5f;
        int segments = Mathf.CeilToInt(archetype.FovDegrees / 2f);
        float step = archetype.FovDegrees / segments;

        Vector3 previous = origin + Quaternion.AngleAxis(-halfFov, Vector3.up) * flatForward * archetype.ViewDistance;
        Gizmos.DrawLine(origin, previous);

        for (int i = 1; i <= segments; i++)
        {
            float angle = -halfFov + i * step;
            Vector3 point = origin + Quaternion.AngleAxis(angle, Vector3.up) * flatForward * archetype.ViewDistance;
            Gizmos.DrawLine(previous, point);
            previous = point;
        }

        Gizmos.DrawLine(origin, previous);
    }

    private void DrawRays()
    {
        if (!rays) return;
        if (!perception.TryGetLatest(out _)) return;
        if (perception.Targets == null) return;

        IReadOnlyList<bool> clear = perception.DebugPointClear;
        if (clear == null) return;

        NpcArchetype archetype = perception.Archetype;
        IReadOnlyList<PerceptionPoint> points = perception.Targets.AllPoints;
        Vector3 eye = perception.EyePosition;

        for (int i = 0; i < points.Count; i++)
        {
            PerceptionPoint point = points[i];

            if (archetype.RegionWeight(point.Region) <= 0f)
                Gizmos.color = ignoredRayColor;
            else
                Gizmos.color = clear[i] ? clearRayColor : blockedRayColor;

            Gizmos.DrawLine(eye, point.WorldPoint);
        }
    }

    private void OnGUI()
    {
        if (!readouts || !TryGetSuspicionAndPerception()) return;

        GUILayout.BeginArea(readoutRect, GUI.skin.box);
        GUILayout.Label(name);

        if (perception.TryGetLatest(out Observation observation))
        {
            GUILayout.Label($"visibility  {observation.visibility:P0}");
            GUILayout.Label($"fDistance   {observation.fDistance:F2}");
            GUILayout.Label($"fAngle      {observation.fAngle:F2}");
            GUILayout.Label($"fLight      {observation.fLight:F2}");
            GUILayout.Label($"fStance     {observation.fStance:F2}");
            GUILayout.Label($"conduct     {observation.conduct}");
            GUILayout.Label($"abnormality {observation.abnormality:F2}");

            IReadOnlyList<float> fractions = perception.DebugRegionFraction;

            foreach (BodyRegion region in Regions)
            {
                float weight = perception.Archetype.RegionWeight(region);
                float fraction = fractions[(int)region];

                GUI.contentColor = weight <= 0f
                    ? ignoredRayColor
                    : Color.Lerp(blockedRayColor, clearRayColor, fraction);

                string value = weight <= 0f ? "ignored" : $"{fraction:P0}";
                GUILayout.Label($"{region.ToString().PadRight(10)} {value}");
            }

            GUI.contentColor = Color.white;
        }
        else
        {
            GUILayout.Label("no observation");
        }

        if (suspicion != null)
        {
            GUILayout.Label($"suspicion   {suspicion.Suspicion:F2}");
            GUILayout.Label($"state       {suspicion.State}");
        }

        GUILayout.EndArea();
    }

    private bool TryGetSuspicionAndPerception()
    {
        if (perception == null)
            perception = GetComponent<NpcPerception>();

        if (suspicion == null)
            suspicion = GetComponent<NpcSuspicion>();

        return perception != null && perception.Archetype != null;
    }
}
