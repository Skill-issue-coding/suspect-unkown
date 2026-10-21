using UnityEngine;

// Holds the player's active skills and gear levels. Reads unlocked flags from
// the save file on Awake, but keeps inspector toggles so skills can be tested in isolation.
public class PlayerSkills : MonoBehaviour
{
    [Header("Lockpicking")]
    [SerializeField, Range(0, 3), Tooltip("Highest lock level the player may pick. 0 cannot pick at all")]
    private int lockpickingLevel = 1;

    [Header("Skills")]
    [SerializeField, Tooltip("Lowers noise radius when moving.")]
    private bool hasSoftSteps;

    [SerializeField, Tooltip("NPCs lose 10% final confidence (multiplier 0.9).")]
    private bool hasForgettable;

    [SerializeField, Tooltip("Caps loot pickup hold time to at most 1.5 seconds.")]
    private bool hasQuickHands;

    public int LockpickingLevel => lockpickingLevel;
    public bool HasSoftSteps => hasSoftSteps;
    public bool HasForgettable => hasForgettable;
    public bool HasQuickHands => hasQuickHands;

    private void Awake()
    {
        ProgressSave save = ProgressStore.Load();
        if (save != null)
        {
            // Bitwise OR so any checkbox ticked in the Inspector remains active for testing
            hasSoftSteps |= save.Has(ProgressUnlock.Skill_SoftSteps);
            hasForgettable |= save.Has(ProgressUnlock.Skill_Forgettable);
            hasQuickHands |= save.Has(ProgressUnlock.Skill_QuickHands);
        }
        // Apply Forgettable factor to the witness confidence calculation
        FeatureBelief.ConfidenceMultiplier = hasForgettable ? 0.9f : 1.0f;
    }
}
