using System;
using UnityEngine;

[Serializable]
public struct AnomalyWeights
{
    [Range(0f, 1f)] public float openDoor;

    public readonly float Get(AnomalyKind kind) => kind switch
    {
        AnomalyKind.OpenDoor => openDoor,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public AnomalyWeights(float openDoor) => this.openDoor = openDoor;
}
