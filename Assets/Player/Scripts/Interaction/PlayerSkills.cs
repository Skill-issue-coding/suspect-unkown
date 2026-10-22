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

    [SerializeField, Tooltip("The police keep less of the job's evidence: see forgettableEvidence")]
    private bool hasForgettable;

    [SerializeField, Range(0f, 1f), Tooltip("With Forgettable, the share of the job's merged evidence that reaches the police description. 0.8 makes a sure description take about 1.25 times as long to build")]
    private float forgettableEvidence = 0.8f;

    [SerializeField, Tooltip("Caps loot pickup hold time to at most 1.5 seconds.")]
    private bool hasQuickHands;

    public int LockpickingLevel => lockpickingLevel;
    public bool HasSoftSteps => hasSoftSteps;
    public bool HasForgettable => hasForgettable;
    public bool HasQuickHands => hasQuickHands;

    [Header("Gear")]
    [SerializeField, Tooltip("Allows breaking of normal glass (level 1)")]
    private bool hasHammer;

    [SerializeField, Tooltip("Muffles the noise when breaking level 1 glass")]
    private bool hasGlassBreaker;

    [SerializeField, Tooltip("Allows cutting of level 2 glass")]
    private bool hasGlassCutter;

    public bool HasHammer => hasHammer;
    public bool HasGlassBreaker => hasGlassBreaker;
    public bool HasGlassCutter => hasGlassCutter;
    // Read by JobWitnesses at job end. Scales the job's merged evidence, not confidence when it is read, so
    // descriptions saved before the skill was bought keep their strength
    public float WitnessEvidenceFactor => hasForgettable ? forgettableEvidence : 1f;

    private void Awake()
    {
        ProgressSave save = ProgressStore.Load();
        if (save != null)
        {
            // Lockpick levels: player starts at 1, upgraded by Lockpick 2 & 3
            if (save.Has(ProgressUnlock.Gear_Lockpick3)) lockpickingLevel = Mathf.Max(lockpickingLevel, 3);
            else if (save.Has(ProgressUnlock.Gear_Lockpick2)) lockpickingLevel = Mathf.Max(lockpickingLevel, 2);
            else if (save.Has(ProgressUnlock.Gear_Lockpick)) lockpickingLevel = Mathf.Max(lockpickingLevel, 1);

            // Gear tools
            hasHammer |= save.Has(ProgressUnlock.Gear_Hammer);
            hasGlassBreaker |= save.Has(ProgressUnlock.Gear_GlassBreaker);
            hasGlassCutter |= save.Has(ProgressUnlock.Gear_GlassCutter);

            // Skills
            hasSoftSteps |= save.Has(ProgressUnlock.Skill_SoftSteps);
            hasForgettable |= save.Has(ProgressUnlock.Skill_Forgettable);
            hasQuickHands |= save.Has(ProgressUnlock.Skill_QuickHands);
        }
    }
}
