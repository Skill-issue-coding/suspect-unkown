using UnityEngine;

// The interaction behaviour for a safe: begins safe cracking on the player. Lives on the safe root
// beside Safe and its Interactable
[RequireComponent(typeof(Safe))]
public class SafeInteraction : MonoBehaviour, IInteractionBehaviour
{
    private Safe safe;

    private void Awake()
    {
        safe = GetComponent<Safe>();
    }

    // Any player can try a safe; an open one offers nothing
    public bool IsBlockedFor(GameObject interactor) => safe.IsOpen;

    public void Interact(GameObject interactor)
    {
        if (safe.IsOpen) return;

        PlayerSafeCracking cracking = interactor.GetComponent<PlayerSafeCracking>();
        if (cracking != null)
            cracking.Begin(safe);
    }
}
