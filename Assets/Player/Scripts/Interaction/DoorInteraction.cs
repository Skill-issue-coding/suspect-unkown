using UnityEngine;

// The interaction behaviour for doors: opens an unlocked door, or begins lockpicking
// if the lock is within the player's skill. Lives on the door root beside Door
[RequireComponent(typeof(Door))]
public class DoorInteraction : MonoBehaviour, IInteractionBehaviour
{
    private Door door;
    private string blockedReason;
    public string BlockedText => blockedReason;

    private void Awake()
    {
        door = GetComponent<Door>();
    }

    // Blocked if locked and the lock level exceeds the player's lockpicking level, a message will be displayed
    public bool IsBlockedFor(GameObject interactor)
    {
        blockedReason = null;
        if (!door.IsLocked) return false;

        PlayerSkills skills = interactor.GetComponent<PlayerSkills>();
        if (skills != null && door.LockLevel <= skills.LockpickingLevel) return false;

        blockedReason = $"Lockpicking level {door.LockLevel} needed";
        return true;
    }

    // Called on press (HoldSeconds is 0)
    public void Interact(GameObject interactor)
    {
        if (!door.IsLocked)
        {
            door.Open();
            return;
        }

        if (IsBlockedFor(interactor)) return;

        PlayerLockpicking lockpicking = interactor.GetComponent<PlayerLockpicking>();
        if (lockpicking != null)
            lockpicking.Begin(door);
    }
}