using UnityEngine;

// The interaction behaviour for cuttable glass: begins a glass-cutting session on the player, and
// removes the pane once it is cut. Lives beside Interactable on the glass
public class GlassCutterInteraction : MonoBehaviour, IInteractionBehaviour
{
    [SerializeField, Tooltip("Optional mesh/object disabled when the glass is fully cut")]
    private GameObject glassPane;

    // Glass cutting is currently not blocked by level/skills
    public bool IsBlockedFor(GameObject interactor) => false;

    public void Interact(GameObject interactor)
    {
        PlayerGlassCutting cutting = interactor.GetComponent<PlayerGlassCutting>();
        if (cutting != null)
            cutting.Begin(this);
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
    }
}
