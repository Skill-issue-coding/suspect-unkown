using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

// A point of interest guards serve (decision 15). Coverage fills while any guard is inside the radius and 
// drains at Importance = baseDecayRate × DecayMultiplier × AlarmFactor. Level design sets the base rate; the
// Phase 8 security plan sets the multiplier; an alarm in the job (PoiSafeAlarm) sets the factor. The POI never learns. Guards report presence with Occupy() and claim 
// with TryClaim(), so two guards never head for the same POI.
public class PointOfInterest : MonoBehaviour
{
    private static readonly List<PointOfInterest> all = new();
    public static IReadOnlyList<PointOfInterest> All => all;

    [SerializeField, Range(0.5f, 5f), Tooltip("The guard covers the POI while within this many metres")]
    private float radius = 2f;

    [SerializeField, Range(0f, 1f), Tooltip("Coverage gained per second while a guard is inside. 1 / fillRate = seconds from empty to full")]
    private float fillRate = 0.5f;

    [SerializeField, Range(0f, 1f), Tooltip("Coverage lost per second: the level design's importance. 1 / rate = seconds from full to empty")]
    private float baseDecayRate = 0.02f;

    [SerializeField, Range(0f, 20f), Tooltip("Minimum seconds a guard stays after arriving")]
    private float dwellTime = 3f;

    [SerializeField, Range(0f, 1f), Tooltip("Coverage at job start")]
    private float initialCoverage = 0.5f;

    public float Coverage { get; private set; }

    public GuardBehaviour ClaimedBy { get; private set; }

    private float decayMultiplier = 1f;
    public float DecayMultiplier
    {
        get => decayMultiplier;
        set => decayMultiplier = Mathf.Max(0f, value);
    }

    // Raised during the job by something the player did here (the office safe opened, thrift shop G3). Kept
    // apart from DecayMultiplier, which the security plan owns, so neither overwrites the other. Only goes up
    public float AlarmFactor { get; private set; } = 1f;

    public float Importance => baseDecayRate * decayMultiplier * AlarmFactor;
    public float DwellTime => dwellTime;

    private bool occupiedThisFrame;

    private void Awake() => Coverage = initialCoverage;

    private void OnEnable() => all.Add(this);
    private void OnDisable()
    {
        all.Remove(this);
        ClaimedBy = null;
    }

    public bool Contains(Vector3 position) => (position - transform.position).sqrMagnitude <= radius * radius;

    public void Occupy() => occupiedThisFrame = true;

    public void RaiseAlarm(float factor) => AlarmFactor = Mathf.Max(AlarmFactor, factor);

    private void LateUpdate()
    {
        float rate = occupiedThisFrame ? fillRate : -Importance;
        Coverage = Mathf.Clamp01(Coverage + rate * Time.deltaTime);
        occupiedThisFrame = false;
    }

    public bool TryClaim(GuardBehaviour guard)
    {
        if (ClaimedBy != null && ClaimedBy != guard) return false;
        ClaimedBy = guard;
        return true;
    }

    public void Release(GuardBehaviour guard) { if (ClaimedBy == guard) ClaimedBy = null; }

    private void OnDrawGizmos()
    {
        Gizmos.color = Application.isPlaying
            ? Color.Lerp(Color.red, Color.green, Coverage)
            : new Color(0.3f, 0.6f, 1f);

        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, new Vector3(1f, 0.01f, 1f));
        Gizmos.DrawWireSphere(Vector3.zero, radius);
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawSphere(transform.position, 0.15f);

        Vector3 forward = transform.forward * radius;
        Gizmos.DrawLine(transform.position, transform.position + forward);
        Gizmos.DrawSphere(transform.position + forward, 0.08f);

#if UNITY_EDITOR
        string coverage = Application.isPlaying ? $"{Coverage:P0}" : $"start {initialCoverage:P0}";
        Handles.Label(transform.position + Vector3.up, $"{name}  {coverage}  x{Importance:F3}");
#endif
    }
}
