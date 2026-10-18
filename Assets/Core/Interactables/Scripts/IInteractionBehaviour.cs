using UnityEngine;

// The contract every interaction type fulfils: whether this interactor is blocked from using it,
// and what happens when it is used.
public interface IInteractionBehaviour
{
    // True when the prompt should grey out (a lock above the player's skill). Checked on trigger
    // events only, never per frame
    bool IsBlockedFor(GameObject interactor);

    // What happens after the press or hold completes (take loot, open/pick a door, start the cutter)
    void Interact(GameObject interactor);
}
