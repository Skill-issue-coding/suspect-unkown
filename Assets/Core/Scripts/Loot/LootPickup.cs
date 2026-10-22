using System;
using UnityEngine;

// A stealable item in the level: a trigger on the Volume layer the player stands in, a prompt and
// an outline the player side shows, and Take(), which announces the item and hides it. Taken is
// static so a level's objectives hear every pickup without keeping a list of them
public class LootPickup : MonoBehaviour, IInteractionBehaviour
{
    public static event Action<LootPickup> Taken;

    [SerializeField, Tooltip("Which item this is. Shared with every pickup of the same item")]
    private LootItem item;

    public LootItem Item => item;
    private string blockedReason;
    public string BlockedText => blockedReason;

    public bool IsBlockedFor(GameObject interactor)
    {
        if (item == null) return false;

        // Bag capacity check
        if (item.CarriedIn == LootCarry.Bag && LootBag.Active != null && LootBag.Active.IsFull)
        {
            blockedReason = "Bag full";
            return true;
        }

        blockedReason = null;
        return false;
    }

    private void Awake()
    {
        if (item == null)
        {
            Debug.LogWarning($"{name} has no LootItem; it cannot be taken", this);
            enabled = false;
            return;
        }

        Interactable interactable = GetComponent<Interactable>();
        if (interactable != null)
        {
            float seconds = item.TakeSeconds;

            // Check save or inspector toggle on PlayerSkills
            PlayerSkills playerSkills = FindAnyObjectByType<PlayerSkills>();
            bool quickHands = (playerSkills != null && playerSkills.HasQuickHands)
                           || ProgressStore.Load().Has(ProgressUnlock.Skill_QuickHands);

            if (quickHands)
                seconds = Mathf.Min(seconds, 2f);

            interactable.SetHoldSeconds(seconds);
        }
    }

    public void Interact(GameObject interactor) => Take();

    // Announce first, then hide: listeners can still read the item. Switching off sends no
    // OnTriggerExit, so the player side must drop this pickup when it hears Taken
    [ContextMenu("Take")]
    public void Take()
    {
        if (!enabled || !gameObject.activeSelf) return;
        Taken?.Invoke(this);
        gameObject.SetActive(false);
    }
}
