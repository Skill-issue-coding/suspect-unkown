using UnityEngine;

// The interaction behaviour for cuttable glass: begins the glass cutter minigame,
// sets PlayerInteraction.IsBusy while active, and disables the interactable when cut
public class GlassCutterInteraction : MonoBehaviour, IInteractionBehaviour
{
    [SerializeField, Tooltip("The minigame controller in the scene (or on this object)")]
    private GlassCutterMinigame minigame;

    [SerializeField, Tooltip("Optional mesh/object disabled when the glass is fully cut")]
    private GameObject glassPane;

    private PlayerInteraction activeInteraction;

    private void Awake()
    {
        if (minigame == null)
            minigame = GetComponentInChildren<GlassCutterMinigame>(true);

        if (minigame == null)
            Debug.LogWarning($"{name} has no GlassCutterMinigame assigned or in children", this);
    }

    // Glass cutting is currently not blocked by level/skills 
    public bool IsBlockedFor(GameObject interactor) => false;

    public void Interact(GameObject interactor)
    {
        if (minigame == null) return;

        activeInteraction = interactor.GetComponent<PlayerInteraction>();
        if (activeInteraction != null)
            activeInteraction.IsBusy = true;

        minigame.Completed += OnCutCompleted;
        minigame.Canceled += OnCutCanceled;

        minigame.Begin();
    }

    private void OnCutCompleted()
    {
        Cleanup();

        if (glassPane != null)
            glassPane.SetActive(false);

        // Turn off this interactable so it cannot be cut again.
        // SetReachable(false) automatically raises Withdrawn to clear player reach!
        Interactable interactable = GetComponent<Interactable>();
        if (interactable != null)
            interactable.SetReachable(false);
    }

    private void OnCutCanceled()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        if (minigame != null)
        {
            minigame.Completed -= OnCutCompleted;
            minigame.Canceled -= OnCutCanceled;
        }

        if (activeInteraction != null)
        {
            activeInteraction.IsBusy = false; // Setting false automatically calls Refresh()
            activeInteraction = null;
        }
    }
}
