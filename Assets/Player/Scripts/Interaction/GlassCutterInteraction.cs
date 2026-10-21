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

    private string blockedReason;
    public string BlockedText => blockedReason;
    public int GlassTier => glassTier;

    private void Awake()
    {
        // Keep the loot inside unreachable while the glass barrier is intact
        if (enableOnDone != null)
            enableOnDone.SetReachable(false);
    }

    // Glass cutting is currently not blocked by level/skills
    public bool IsBlockedFor(GameObject interactor)
    {
        PlayerSkills skills = interactor != null ? interactor.GetComponent<PlayerSkills>() : null;
        if (skills == null) return true;

        // Tier 1 needs the hammer
        if (glassTier == 1 && !skills.HasHammer)
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

        PlayerSkills skills = interactor != null ? interactor.GetComponent<PlayerSkills>() : null;

        if (glassTier == 1)
        {
            // smash normal glass
            bool hasBreaker = skills != null && skills.HasGlassBreaker;
            float radius = hasBreaker ? 6f : 12f;
            float alarm = hasBreaker ? 1f : 1f;

            NoiseBus.Publish(new NoiseEvent(transform.position, radius, alarm));
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
