// A sound, as the source describes it. Listeners decide whether it reached them: radius is how far it
// carries through open space along the NavMesh, and alarm is how suspicious it is on its own
// (0 harmless, 1 unmistakable), so ordinary footsteps can be heard without raising anything.
using UnityEngine;

public readonly struct NoiseEvent
{
    public readonly Vector3 Position;
    public readonly float Radius;
    public readonly float Alarm;

    public NoiseEvent(Vector3 position, float radius, float alarm)
    {
        Position = position;
        Radius = Mathf.Max(0f, radius);
        Alarm = Mathf.Clamp01(alarm);
    }
}
