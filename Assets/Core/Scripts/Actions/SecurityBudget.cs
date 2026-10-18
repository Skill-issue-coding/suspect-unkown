using System.Collections.Generic;
using UnityEngine;

// A level's adaptive-security budget as one shared asset, read by the level (SecurityLayout) 
// to know how many guards and POIs to add, and by the main menu to show raised security. It 
// holds no runtime state.
[CreateAssetMenu(fileName = "SecurityBudget", menuName = "Suspect Unknown/Security Budget")]
public class SecurityBudget : ScriptableObject
{
    [SerializeField, Tooltip("Guards and adaptive POIs per completed-run count; the tier from 0 is the first run")]
    private SecurityTier[] tiers = { };

    public IReadOnlyList<SecurityTier> Tiers => tiers;

    public SecurityTier TierFor(int completedRuns) => SecurityTier.For(tiers, completedRuns);

    // Raised once the level has more guards than a first run has. Compared with the tier at 0, not a
    // fixed count, so a level that starts with three guards is not raised until it hires a fourth
    public bool IsRaised(int completedRuns) => TierFor(completedRuns).guards > TierFor(0).guards;

    // The rules that depend only on the tiers. Guards against spawns and POIs against candidates need
    // the scene, so SecurityLayout checks those
    private void OnValidate()
    {
        if (tiers == null || tiers.Length == 0)
        {
            Debug.LogWarning($"{name}: no tiers, a level using this budget has no guards", this);
            return;
        }

        bool hasFirstRun = false;
        for (int i = 0; i < tiers.Length; i++)
        {
            SecurityTier tier = tiers[i];
            if (tier.fromCompletedRuns == 0) hasFirstRun = true;
            if (tier.guards < 1)
                Debug.LogWarning($"{name}: tier {i} has no guards", this);
            if (tier.adaptivePois < 0)
                Debug.LogWarning($"{name}: tier {i} has a negative number of adaptive POIs", this);
            if (i > 0 && tier.fromCompletedRuns <= tiers[i - 1].fromCompletedRuns)
                Debug.LogWarning($"{name}: tier {i} is not after tier {i - 1}; keep tiers sorted by completed runs", this);
            if (i > 0 && tier.guards < tiers[i - 1].guards)
                Debug.LogWarning($"{name}: tier {i} has fewer guards than tier {i - 1}; the guard count never shrinks", this);
        }

        if (!hasFirstRun)
            Debug.LogWarning($"{name}: no tier starts at 0 completed runs, a first run has no budget", this);
    }
}
