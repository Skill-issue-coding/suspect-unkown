using System.Collections.Generic;
using UnityEngine;

public class PerceptionTargets : MonoBehaviour
{
    private readonly List<PerceptionPoint> points = new();

    public IReadOnlyList<PerceptionPoint> Points => points;

    private void Awake()
    {
        PerceptionPoint[] all = GetComponentsInChildren<PerceptionPoint>(true);
        Dictionary<BodyRegion, PerceptionPoint> best = new Dictionary<BodyRegion, PerceptionPoint>();

        foreach (PerceptionPoint point in all)
        {
            if (!best.TryGetValue(point.Region, out PerceptionPoint current) || point.Priority > current.Priority)
            {
                best[point.Region] = point;
            }
        }

        points.AddRange(best.Values);
    }
}
