using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// A place where civilians of one role spend time: a shelf to browse, the register, the office. Passive:
// CivilianBehaviour reads the registry and claims a free spot, so two civilians never share one. The
// spot's forward is the way the civilian faces while there. Not a PointOfInterest: those are guard
// security coverage that Phase 8 moves around; civilians stay out of that model
public class CivilianSpot : MonoBehaviour
{
    private const float GizmoRadius = 1.5f;
    private static readonly List<CivilianSpot> all = new();
    public static IReadOnlyList<CivilianSpot> All => all;

    [SerializeField, Tooltip("Who uses this spot")]
    private CivilianRole role = CivilianRole.Customer;

    [SerializeField, Tooltip("Minimum and maximum seconds a civilian stays, picked at random each visit")]
    private Vector2 dwellSeconds = new Vector2(4f, 10f);

    [SerializeField, Tooltip("Which dwell clip a civilian plays here")]
    private CivilianStation station = CivilianStation.Shelf;

    public CivilianRole Role => role;
    public CivilianBehaviour ClaimedBy { get; private set; }

    public float RandomDwell() => Random.Range(dwellSeconds.x, dwellSeconds.y);

    public CivilianStation Station => station;

    public bool TryClaim(CivilianBehaviour civilian)
    {
        if (ClaimedBy != null && ClaimedBy != civilian) return false;

        ClaimedBy = civilian;
        return true;
    }

    public void Release(CivilianBehaviour civilian)
    {
        if (ClaimedBy == civilian) ClaimedBy = null;
    }

    private void OnEnable() => all.Add(this);

    private void OnDisable()
    {
        all.Remove(this);
        ClaimedBy = null;
    }

    private void OnValidate()
    {
        if (dwellSeconds.x > dwellSeconds.y)
            Debug.LogWarning($"{name} dwellSeconds minimum is above its maximum", this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = role switch
        {
            CivilianRole.Customer => new Color(0.3f, 0.9f, 0.3f),
            CivilianRole.Employee => new Color(0.3f, 0.6f, 1f),
            _ => new Color(0.7f, 0.4f, 1f),
        };

        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, new Vector3(1f, 0.01f, 1f));
        Gizmos.DrawWireSphere(Vector3.zero, GizmoRadius);
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawSphere(transform.position, 0.1f);

        Vector3 forward = transform.forward * GizmoRadius * 1.5f;
        Gizmos.DrawLine(transform.position, transform.position + forward);
        Gizmos.DrawSphere(transform.position + forward, 0.06f);

        if (ClaimedBy != null)
            Gizmos.DrawLine(transform.position, ClaimedBy.transform.position);

#if UNITY_EDITOR
        string claim = ClaimedBy != null ? $"  → {ClaimedBy.name}" : "";
        Handles.Label(transform.position + Vector3.up * 0.6f, $"{role}  {dwellSeconds.x:F0}–{dwellSeconds.y:F0}s{claim}");
#endif
    }
}
