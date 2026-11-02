using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Decides whether a published sound reached this NPC, measuring distance along the NavMesh so sound
// travels around corners and through open doors, never through walls or closed doors (decision 10): the
// path leaves out the Door area, the NavMeshLinks guards cross closed doors by. Cheap straight-line
// rejection first; a path is only calculated for sounds that could be in range. Every enabled listener is
// also kept in a static list, so NoiseRings can show how far the best ear in the level hears.
[RequireComponent(typeof(NpcPerception))]
public class NpcHearing : MonoBehaviour
{
    public enum Verdict { None, Heard, OffNavMesh, NoPath, TooFar }

    // Corners a path may have before it counts as too far. Real rooms need far fewer
    private const int MaxCorners = 32;

    // Metres from the sound's position to search for the nearest NavMesh point
    private const float SampleDistance = 1f;

    public event Action<HeardNoise> Heard;

    // Every enabled listener. Holds the components, not their sensitivities: a spawner may swap the
    // archetype after OnEnable (NpcPerception.AssignArchetype), so the value is read when it is asked for
    private static readonly List<NpcHearing> listening = new List<NpcHearing>();

    private NpcPerception perception;
    private NavMeshPath path;

    // Sound travels the NavMesh but not across door links, so a closed door blocks it (decision 10); an open
    // door is plain floor and lets it through
    private int areaMask = NavMesh.AllAreas;

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

        // 1 << door is the area's bit; & ~ clears it, leaving every area but Door
        int door = NavMesh.GetAreaFromName("Door");
        if (door < 0) Debug.LogWarning($"{name}: no NavMesh area 'Door'; sound will pass through closed doors", this);
        else areaMask = NavMesh.AllAreas & ~(1 << door);
    }

    private void OnEnable()
    {
        NoiseBus.Published += OnNoise;
        listening.Add(this);
    }

    private void OnDisable()
    {
        NoiseBus.Published -= OnNoise;
        listening.Remove(this);
    }

    // The highest hearing sensitivity among enabled listeners, 0 when there are none. Loops a handful of
    // NPCs per call, so it is asked once per noise, never per frame
    public static float HighestSensitivity()
    {
        float highest = 0f;
        for (int i = 0; i < listening.Count; i++)
        {
            NpcArchetype archetype = listening[i].perception.Archetype;
            if (archetype != null && archetype.HearingSensitivity > highest)
                highest = archetype.HearingSensitivity;
        }
        return highest;
    }

    private void OnNoise(NoiseEvent noise)
    {
        NpcArchetype archetype = perception.Archetype;
        if (archetype == null) return;

        float reach = noise.Radius * archetype.HearingSensitivity;
        if (reach <= 0f) return;

        // A path is never shorter than the straight line, so this rejects most sounds for free
        Vector3 listener = transform.position;
        if ((noise.Position - listener).sqrMagnitude > reach * reach) return;

        if (!NavMesh.SamplePosition(noise.Position, out NavMeshHit hit, SampleDistance, areaMask))
        {
#if UNITY_EDITOR
            Record(Verdict.OffNavMesh, noise, reach, 0f, 0);
#endif
            return;
        }
        if (!NavMesh.CalculatePath(listener, hit.position, areaMask, path)
            || path.status != NavMeshPathStatus.PathComplete)
        {
#if UNITY_EDITOR
            Record(Verdict.NoPath, noise, reach, 0f, 0);
#endif
            return;
        }

        float length = PathLength(reach, out int cornerCount);
#if UNITY_EDITOR
        Record(length > reach ? Verdict.TooFar : Verdict.Heard, noise, reach, length, cornerCount);
#endif
        if (length > reach) return;

        Heard?.Invoke(new HeardNoise(noise.Position, 1f - length / reach, noise.Alarm));
    }

    private float PathLength(float limit, out int count)
    {
        count = path.GetCornersNonAlloc(corners);
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
