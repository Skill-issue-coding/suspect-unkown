using UnityEngine;

// A sound as one NPC heard it. Loudness is 1 at the source and 0 at the edge of what this NPC can
// hear, measured along the NavMesh. Alarm is copied from the NoiseEvent.
public readonly struct HeardNoise
{
    public readonly Vector3 Position;
    public readonly float Loudness;
    public readonly float Alarm;

    public HeardNoise(Vector3 position, float loudness, float alarm)
    {
        Position = position;
        Loudness = loudness;
        Alarm = alarm;
    }
}
