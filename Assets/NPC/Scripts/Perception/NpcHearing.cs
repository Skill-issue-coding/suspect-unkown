using System;
using UnityEngine;
using UnityEngine.AI;

// Decides whether a published sound reached this NPC, measuring distance along the NavMesh so sound
// travels around corners and through open doors, never through walls (decision 10). Cheap straight-line
// rejection first; a path is only calculated for sounds that could be in range.
[RequireComponent(typeof(NpcPerception))]
public class NpcHearing : MonoBehaviour
{
    public enum Verdict { None, Heard, OffNavMesh, NoPath, TooFar }

    // Corners a path may have before it counts as too far. Real rooms need far fewer
    private const int MaxCorners = 32;

    // Metres from the sound's position to search for the nearest NavMesh point
    private const float SampleDistance = 1f;

    public event Action<HeardNoise> Heard;

    private NpcPerception perception;
    private NavMeshPath path;
    private readonly Vector3[] corners = new Vector3[MaxCorners];

#if UNITY_EDITOR
    public Verdict DebugVerdict { get; private set; }
    public float DebugTime { get; private set; } = float.NegativeInfinity;
    public float DebugPathLength { get; private set; }
    public float DebugReach { get; private set; }
    public float DebugLoudness { get; private set; }
    public float DebugAlarm { get; private set; }

    private readonly Vector3[] debugCorners = new Vector3[MaxCorners];
    private int debugCount;
    private Vector3 debugSource;

    private void Record(Verdict verdict, NoiseEvent noise, float reach, float length, int cornerCount)
    {
        DebugVerdict = verdict;
        DebugTime = Time.time;
        DebugReach = reach;
        DebugPathLength = length;
        DebugAlarm = noise.Alarm;
        DebugLoudness = verdict == Verdict.Heard ? 1f - length / reach : 0f;
        debugSource = noise.Position;
        debugCount = cornerCount;
        System.Array.Copy(corners, debugCorners, cornerCount);
    }
#endif

    private void Awake()
    {
        perception = GetComponent<NpcPerception>();
        path = new NavMeshPath();
    }

    private void OnEnable() => NoiseBus.Published += OnNoise;
    private void OnDisable() => NoiseBus.Published -= OnNoise;

    private void OnNoise(NoiseEvent noise)
    {
        NpcArchetype archetype = perception.Archetype;
        if (archetype == null) return;

        float reach = noise.Radius * archetype.HearingSensitivity;
        if (reach <= 0f) return;

        // A path is never shorter than the straight line, so this rejects most sounds for free
        Vector3 listener = transform.position;
        if ((noise.Position - listener).sqrMagnitude > reach * reach) return;

        if (!NavMesh.SamplePosition(noise.Position, out NavMeshHit hit, SampleDistance, NavMesh.AllAreas))
        {
#if UNITY_EDITOR
            Record(Verdict.OffNavMesh, noise, reach, 0f, 0);
#endif
            return;
        }
        if (!NavMesh.CalculatePath(listener, hit.position, NavMesh.AllAreas, path)) return;
        if (path.status != NavMeshPathStatus.PathComplete) return;

        float length = PathLength(reach);
        if (length > reach) return;

        Heard?.Invoke(new HeardNoise(noise.Position, 1f - length / reach, noise.Alarm));
    }

    private float PathLength(float limit)
    {
        int count = path.GetCornersNonAlloc(corners);
        if (count >= MaxCorners) return float.PositiveInfinity;

        float length = 0f;
        for (int i = 1; i < count; i++)
        {
            length += Vector3.Distance(corners[i - 1], corners[i]);
            if (length > limit) return length;
        }
        return length;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (Time.time - DebugTime > 1.5f) return;

        Gizmos.color = DebugVerdict == Verdict.Heard ? Color.green : Color.red;
        for (int i = 1; i < debugCount; i++)
            Gizmos.DrawLine(debugCorners[i - 1] + Vector3.up * 0.1f, debugCorners[i] + Vector3.up * 0.1f);
        Gizmos.DrawWireSphere(debugSource, 0.25f);
    }
#endif
}
