using UnityEngine;

// The interaction behaviour for physical glass.
// Tier 1 (Normal): Smashed with a hammer (loud) or glass breaker (muffled).
// Tier 2 (Strong): Cut with the glass cutter via the minigame.
// Once broken/cut, enables the loot inside so it can be picked up.
public class GlassCutterInteraction : MonoBehaviour, IInteractionBehaviour
{
    [SerializeField, Range(1, 2), Tooltip("1 = normal glass (hammer / glass breaker), 2 = strong glass (glass cutter)")]
    private int glassTier = 1;
    [SerializeField, Tooltip("Optional mesh/object disabled when the glass is fully cut")]
    private GameObject glassPane;

    [SerializeField, Tooltip("Component to enable after glass is broken/cut (the loot inside)")]
    private Interactable enableOnDone;

    [Header("Hammer Specifics")]
    [SerializeField, Min(0f), Tooltip("Noise radius for hammer")]
    private float hammerRadius = 12f;

    [SerializeField, Min(0f), Tooltip("Noise alarm for hammer")]
    private float hammerAlarm = 1f;

    [Header("Glass Breaker Specifics")]
    [SerializeField, Min(0f), Tooltip("Noise radius for glass breaker")]
    private float glassBreakerRadius = 6f;

    [SerializeField, Min(0f), Tooltip("Noise alarm for glass breaker")]
    private float glassBreakerAlarm = 1f;

    private GameObject cachedInteractor;
    private PlayerSkills cachedSkills;

    private string blockedReason;
    public string BlockedText => blockedReason;
    public int GlassTier => glassTier;

    private void Awake()
    {
        // Keep the loot inside unreachable while the glass barrier is intact
        if (enableOnDone != null)
            enableOnDone.SetReachable(false);
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

    // Tier 1 needs the hammer or the glass breaker, tier 2 the glass cutter
    public bool IsBlockedFor(GameObject interactor)
    {
        PlayerSkills skills = SkillsOf(interactor);
        if (skills == null) return true;

        if (glassTier == 1 && !skills.HasHammer && !skills.HasGlassBreaker)
        {
            blockedReason = "Needs hammer";
            return true;
        }

        // Tier 2 needs the glass cutter
        if (glassTier == 2 && !skills.HasGlassCutter)
        {
            blockedReason = "Needs glass cutter";
            return true;
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

        if (enableOnDone != null)
            enableOnDone.SetReachable(true);
    }
}
