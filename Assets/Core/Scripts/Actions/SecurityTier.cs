using System;
using UnityEngine;

[Serializable]
public struct SecurityTier
{
    [Tooltip("This tier applies from this many completed runs")]
    public int fromCompletedRuns;

    [Tooltip("Guards spawned")]
    public int guards;

    [Tooltip("Adaptive POIs switched on")]
    public int adaptivePois;

    public static SecurityTier For(SecurityTier[] tiers, int completedRuns)
    {
        SecurityTier result = default;
        if (tiers == null) return result;
        foreach (SecurityTier tier in tiers)
            if (tier.fromCompletedRuns <= completedRuns && tier.fromCompletedRuns >= result.fromCompletedRuns)
                result = tier;
        return result;
    }
}
