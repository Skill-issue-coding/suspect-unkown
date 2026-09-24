using System;
using System.Collections.Generic;
using UnityEngine;

// Exposes every PerceptionPoint on the player, flat and grouped by region. Lists are fixed
// after Awake: NpcPerception indexes AllPoints by position.
[RequireComponent(typeof(PlayerConduct))]
public class PerceptionTargets : MonoBehaviour
{
    private static readonly int RegionCount = Enum.GetValues(typeof(BodyRegion)).Length;
    private readonly List<PerceptionPoint> all = new();
    private List<PerceptionPoint>[] byRegion;

    public IReadOnlyList<PerceptionPoint> AllPoints => all;
    public IReadOnlyList<PerceptionPoint> GetPoints(BodyRegion region) => byRegion[(int)region];

    private void Awake()
    {
        PerceptionPoint[] found = GetComponentsInChildren<PerceptionPoint>(false);

        byRegion = new List<PerceptionPoint>[RegionCount];
        for (int r = 0; r < RegionCount; r++)
            byRegion[r] = new List<PerceptionPoint>();

        foreach (PerceptionPoint point in found)
        {
            all.Add(point);
            byRegion[(int)point.Region].Add(point);
        }

        for (int r = 0; r < RegionCount; r++)
            if (byRegion[r].Count == 0)
                Debug.LogWarning($"{name} has no PerceptionPoint for {(BodyRegion)r}.", this);
    }
}
