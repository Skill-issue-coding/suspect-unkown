using UnityEngine;

// The interaction behaviour for physical glass.
// Tier 1 (Normal): Smashed with the hammer (loud, slow), or quicker and muffled once the glass breaker is owned too.
// Tier 2 (Strong): Cut with the glass cutter via the minigame.
// Once broken/cut, enables the loot inside so it can be picked up.
public class GlassCutterInteraction : MonoBehaviour, IInteractionBehaviour
{
    [SerializeField, Range(1, 2), Tooltip("1 = normal glass (hammer / glass breaker), 2 = strong glass (glass cutter)")]
    private int glassTier = 1;
    [SerializeField, Tooltip("Optional mesh/object disabled when the glass is fully cut")]
    private GameObject glassPane;

    [SerializeField, Tooltip("The loot inside, unreachable until the glass is broken/cut. List every item the case can hold, so whichever one is active this run is unlocked")]
    private Interactable[] enableOnDone;

    [Header("Hammer Specifics")]
    [SerializeField, Min(0f), Tooltip("Noise radius for hammer")]
    private float hammerRadius = 12f;
    [SerializeField, Tooltip("Seconds to hold E to smash normal glass with only the hammer")]
    private float hammerHoldSeconds = 2.5f;
    [SerializeField, Min(0f), Tooltip("Noise alarm for hammer")]
    private float hammerAlarm = 1f;

    [Header("Glass Breaker Specifics")]
    [SerializeField, Min(0f), Tooltip("Noise radius for glass breaker")]
    private float glassBreakerRadius = 6f;
    [SerializeField, Tooltip("Seconds to hold E to smash normal glass with the glass breaker")]
    private float breakerHoldSeconds = 1.5f;
    [SerializeField, Min(0f), Tooltip("Noise alarm for glass breaker")]
    private float glassBreakerAlarm = 1f;

    private GameObject cachedInteractor;
    private PlayerSkills cachedSkills;

    private string blockedReason;
    public string BlockedText => blockedReason;
    public int GlassTier => glassTier;
    private Interactable interactable;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        if (interactable != null)
            interactable.SetHoldSeconds(glassTier == 1 ? hammerHoldSeconds : 0f);
        // Keep the loot inside unreachable while the glass barrier is intact
        SetLootReachable(false);
    }

    // Works on inactive loot too (a variant that did not roll this run), since SetReachable only toggles its trigger
    private void SetLootReachable(bool reachable)
    {
        foreach (Interactable loot in enableOnDone)
            if (loot != null)
                loot.SetReachable(reachable);
    }

    // The interactor is always the player, so its skills are looked up once instead of on every check.
    // IsBlockedFor runs every frame of a hold, where GetComponent does not belong
    private PlayerSkills SkillsOf(GameObject interactor)
    {
        if (interactor != cachedInteractor)
        {
            cachedInteractor = interactor;
            cachedSkills = interactor != null ? interactor.GetComponent<PlayerSkills>() : null;
        }
        return cachedSkills;
    }

    // Tier 1 needs the hammer (the glass breaker is an upgrade bought after it), tier 2 the glass cutter.
    // Also sets the hold time for the tool the player has, since the prompt reads it when shown
    public bool IsBlockedFor(GameObject interactor)
    {
        PlayerSkills skills = SkillsOf(interactor);
        if (skills == null) return true;

        if (glassTier == 1)
        {
            if (!skills.HasHammer)
            {
                blockedReason = "Needs hammer";
                return true;
            }
            if (interactable != null)
                interactable.SetHoldSeconds(skills.HasGlassBreaker ? breakerHoldSeconds : hammerHoldSeconds);
        }

        // Tier 2 needs the glass cutter
        if (glassTier == 2)
        {
            if (!skills.HasGlassCutter)
            {
                blockedReason = "Needs glass cutter";
                return true;
            }
            if (interactable != null)
                interactable.SetHoldSeconds(0f);
        }

        blockedReason = null;
        return false;
    }

    public void Interact(GameObject interactor)
    {
        if (IsBlockedFor(interactor)) return;

        if (glassTier == 1)
        {
            // Smash normal glass. The glass breaker is quieter than the hammer and wins when both are owned
            bool hasBreaker = SkillsOf(interactor).HasGlassBreaker;
            float radius = hasBreaker ? glassBreakerRadius : hammerRadius;
            float alarm = hasBreaker ? glassBreakerAlarm : hammerAlarm;

            // From the player's feet, not the glass: the case sits a metre up inside its own carved hole in the
            // NavMesh, too far from walkable floor for NpcHearing to place the sound, so nobody would hear it
            NoiseBus.Publish(new NoiseEvent(interactor.transform.position, radius, alarm));
            OnCut(); // removes the glass => loot available
        }
        else if (glassTier == 2)
        {
            PlayerGlassCutting cutting = interactor != null ? interactor.GetComponent<PlayerGlassCutting>() : null;
            if (cutting != null)
                cutting.Begin(this);
        }
    }

    // Called by PlayerGlassCutting when the circle is complete
    public void OnCut()
    {
        // Turn off the interactable first so it cannot be cut again. SetReachable(false) raises
        // Withdrawn, which clears the player's reach even when the pane below is this very object
        Interactable interactable = GetComponent<Interactable>();
        if (interactable != null)
            interactable.SetReachable(false);

        if (glassPane != null)
            glassPane.SetActive(false);

        SetLootReachable(true);
    }
}
