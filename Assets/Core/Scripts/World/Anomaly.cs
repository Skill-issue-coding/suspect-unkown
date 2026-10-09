using System.Collections.Generic;
using UnityEngine;

// World state only a robber causes (decision 18). Passive: the owner 
// (a door, the breaker) raises and clears it; NPCs read Raised on their 
// perception tick. Only raised anomalies are registered, so the usual case, 
// nothing wrong, costs NPCs nothing.
public class Anomaly : MonoBehaviour
{
    private static readonly List<Anomaly> raised = new();
    public static IReadOnlyList<Anomaly> Raised => raised;

    [SerializeField, Tooltip("What kind of wrong this is. Archetypes weigh kinds differently")]
    private AnomalyKind kind;

    [SerializeField, Tooltip("Point an NPC must have a clear line to, in this object's local space. For a door, its middle at about chest height")]
    private Vector3 sightOffset = new Vector3(0f, 1.2f, 0f);

    public AnomalyKind Kind => kind;
    public bool IsRaised { get; private set; }
    public Vector3 SightPoint => transform.TransformPoint(sightOffset);

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
        if (!IsRaised) return;
        IsRaised = false;
        raised.Remove(this);
    }

    private void OnDisable() => Clear();

    private void OnDrawGizmos()
    {
        Gizmos.color = IsRaised ? Color.red : new Color(0.6f, 0.6f, 0.6f, 0.5f);
        Gizmos.DrawWireSphere(SightPoint, IsRaised ? 0.25f : 0.1f);
    }
}
