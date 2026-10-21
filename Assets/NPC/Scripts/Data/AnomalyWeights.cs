using System;
using UnityEngine;

// How suspicious each kind of anomaly makes one observer, 0 to 1. One field and one switch arm per
// AnomalyKind value. Weight 0 still lets the NPC notice the anomaly, it just raises no suspicion:
// the tired clerk shuts an open door as a chore (decisions 18 and 22)
[Serializable]
public struct AnomalyWeights
{
    [Range(0f, 1f)] public float openDoor;
    [Range(0f, 1f)] public float safeOpen;

    public readonly float Get(AnomalyKind kind) => kind switch
    {
        AnomalyKind.OpenDoor => openDoor,
        AnomalyKind.SafeOpen => safeOpen,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public AnomalyWeights(float openDoor, float safeOpen) { this.openDoor = openDoor; this.safeOpen = safeOpen; }
}
