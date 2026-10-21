using System.Collections.Generic;
using UnityEngine;

// World state only a robber causes (decision 18). Passive: the owner 
// (a door, the breaker) raises and clears it; NPCs read Raised on their 
// perception tick. Only raised anomalies are registered, so the usual case,
// nothing wrong, costs NPCs nothing. One NPC claims it and asks the owner to
// fix it with Resolve(); the owner does the fixing.
public class Anomaly : MonoBehaviour
{
    private static readonly List<Anomaly> raised = new();
    public static IReadOnlyList<Anomaly> Raised => raised;

    [SerializeField, Tooltip("What kind of wrong this is. Archetypes weigh kinds differently")]
    private AnomalyKind kind;

    [SerializeField, Tooltip("Point an NPC must have a clear line to, in this object's local space. For a door, its middle at about chest height")]
    private Vector3 sightOffset = new Vector3(0f, 1.2f, 0f);

    [SerializeField, Tooltip("Where an NPC stands to deal with this, in local space. On the NavMesh, clear of a swinging leaf")]
    private Vector3 resolveOffset = new Vector3(0f, 0f, -1f);

    public AnomalyKind Kind => kind;
    public bool IsRaised { get; private set; }
    public Vector3 SightPoint => transform.TransformPoint(sightOffset);
    public Vector3 ResolvePoint => transform.TransformPoint(resolveOffset);

    // Who is dealing with it, like a POI claim (decision 15). UnityEngine.Object, not GuardBehaviour,
    // since Core never sees NPC types
    public Object ClaimedBy { get; private set; }

    public event System.Action Resolved;

    [ContextMenu("Raise")]
    public void Raise()
    {
        if (IsRaised) return;
        IsRaised = true;
        raised.Add(this);
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        ClaimedBy = null;
        if (!IsRaised) return;
        IsRaised = false;
        raised.Remove(this);
    }

    private void OnDisable() => Clear();

    public bool TryClaim(Object claimant)
    {
        if (ClaimedBy != null && ClaimedBy != claimant) return false;
        ClaimedBy = claimant;
        return true;
    }

    public void Release(Object claimant) { if (ClaimedBy == claimant) ClaimedBy = null; }

    // The owner does the fixing (the door slams); this only asks
    [ContextMenu("Resolve")]
    public void Resolve() { if (IsRaised) Resolved?.Invoke(); }

    private void OnDrawGizmos()
    {
        Gizmos.color = IsRaised ? Color.red : new Color(0.6f, 0.6f, 0.6f, 0.5f);
        Gizmos.DrawWireSphere(SightPoint, IsRaised ? 0.25f : 0.1f);

        Gizmos.color = Color.greenYellow;
        Gizmos.DrawSphere(ResolvePoint, 0.1f);
        Gizmos.DrawLine(SightPoint, ResolvePoint);
    }
}
