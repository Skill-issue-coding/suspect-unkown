using UnityEngine;
using System;
using System.Collections.Generic;

// Draws what NpcPerception already found; never casts a ray of its own, so what you tune is what
// the NPC used. Cone and rays show when the NPC is selected. Ray colors update at the perception
// tick rate while their endpoints follow the player every frame, so a lag of one tick is expected.
// Grey rays are points in regions the archetype ignores; no ray was cast. In the editor the readout
// shows only for the selected NPC (or a child of it), so a room of NPCs doesn't draw on top of itself.
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
    [SerializeField] private Rect readoutRect = new Rect(10f, 10f, 220f, 602f);

    private NpcPerception perception;
    private NpcSuspicion suspicion;
    private NpcHearing hearing;
    private NpcAnomalySight anomalySight;
    private NpcWitness witness;

    private void OnDrawGizmosSelected()
    {
        if (!TryGetSuspicionAndPerception()) return;

        if (suspicion != null && suspicion.HasStimulus)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(suspicion.LastStimulus, 0.35f);
        }

        DrawCone();
        DrawRays();
    }

    private void DrawCone()
    {
        if (!gizmos) return;

        Gizmos.color = gizmosColor;
        Gizmos.DrawWireSphere(perception.EyePosition, eyeGizmoRadius);

        // The scaled range and FOV, the ones perception tests against, not the archetype's full values
        float range = perception.ViewDistance;
        float fov = perception.FovDegrees;

        Vector3 origin = transform.position;
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        float halfFov = fov * 0.5f;
        // At least one segment, so a very narrow cone never divides by zero below
        int segments = Mathf.Max(1, Mathf.CeilToInt(fov / 2f));
        float step = fov / segments;

        Vector3 previous = origin + Quaternion.AngleAxis(-halfFov, Vector3.up) * flatForward * range;
        Gizmos.DrawLine(origin, previous);

        for (int i = 1; i <= segments; i++)
        {
            float angle = -halfFov + i * step;
            Vector3 point = origin + Quaternion.AngleAxis(angle, Vector3.up) * flatForward * range;
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
#if UNITY_EDITOR
        if (!IsSelected()) return;
#endif

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
            GUILayout.Label($"recognition {observation.recognition:F2} (match {JobWitnesses.RecordMatch(observation.appearance):P0}{(JobWitnesses.IsBurned(observation.appearance) ? ", burned" : "")})");

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

        GUILayout.Label($"view        {perception.RangeScale:P0} range, {perception.FovScale:P0} fov");

        if (suspicion != null)
        {
            GUILayout.Label(suspicion.Baseline > 0f
                ? $"suspicion   {suspicion.Suspicion:F2} (base {suspicion.Baseline:F2})"
                : $"suspicion   {suspicion.Suspicion:F2}");
            GUILayout.Label($"state       {suspicion.State}");
        }

#if UNITY_EDITOR
        if (hearing != null && Time.time - hearing.DebugTime < 3f)
        {
            string result = hearing.DebugVerdict == NpcHearing.Verdict.Heard
                ? $"heard  loud {hearing.DebugLoudness:F2}  alarm {hearing.DebugAlarm:F2}"
                : $"{hearing.DebugVerdict}  {hearing.DebugPathLength:F1} / {hearing.DebugReach:F1} m";
            GUILayout.Label($"sound  {result}");
        }

        if (anomalySight != null && Time.time - anomalySight.DebugTime < 1f && anomalySight.DebugLastNoticed != null)
            GUILayout.Label($"anomaly {anomalySight.DebugLastNoticed.Kind}");
#endif

        if (witness != null)
        {
            GUILayout.Label($"witness  {witness.Exposure:F1} s  (seen {witness.SeenTime:F1} s, best {witness.BestQuality:F2})");

            foreach (FeatureSlot slot in (FeatureSlot[])Enum.GetValues(typeof(FeatureSlot)))
            {
                if (witness.TryDescribe(slot, out OutfitFeature f, out float sc, out float cc))
                    GUILayout.Label($"  {slot,-6} {f.color} {cc:P0} · style {f.style:000} {sc:P0}");
                else
                    GUILayout.Label($"  {slot,-6} —");
            }
        }

        if (suspicion != null && suspicion.HasStimulus)
            GUILayout.Label($"stimulus {Vector3.Distance(transform.position, suspicion.LastStimulus):F1} m away");

        GUILayout.EndArea();
    }

    // Test hooks until ResidentBehaviour drives the scale (B4): the dozing values from apartment-level.md 2.2
    [ContextMenu("Test: Dozing View")]
    private void TestDozingView()
    {
        if (TryGetSuspicionAndPerception()) perception.SetViewScale(0.35f, 0.5f);
    }

    [ContextMenu("Test: Full View")]
    private void TestFullView()
    {
        if (TryGetSuspicionAndPerception()) perception.SetViewScale(1f, 1f);
    }

#if UNITY_EDITOR
    // Selecting the NPC's mesh or indicator counts too, since clicking it in the scene often picks a child
    private bool IsSelected()
    {
        Transform selected = UnityEditor.Selection.activeTransform;
        return selected != null && selected.IsChildOf(transform);
    }
#endif

    private bool TryGetSuspicionAndPerception()
    {
        if (perception == null)
            perception = GetComponent<NpcPerception>();

        if (suspicion == null)
            suspicion = GetComponent<NpcSuspicion>();

        if (hearing == null)
            hearing = GetComponent<NpcHearing>();

        if (anomalySight == null)
            anomalySight = GetComponent<NpcAnomalySight>();

        if (witness == null)
            witness = GetComponent<NpcWitness>();

        return perception != null && perception.Archetype != null;
    }
}
