using UnityEngine;

// The player side of VanStash, as PlayerInteraction is of Interactable: notices the van's stash point,
// shows its G prompt while the bag holds something, and empties the bag on G. One per player
[RequireComponent(typeof(PlayerInputs))]
public class PlayerVanStash : MonoBehaviour
{
    private PlayerInputs input;
    private VanStash near;    // the stash point the player stands in
    private VanStash shown;   // its prompt, while showing

    private void Awake() => input = GetComponent<PlayerInputs>();

    private void OnEnable()
    {
        input.DropPressed += OnDropPressed;
        LootBag.Changed += Refresh;
    }

    private void OnDisable()
    {
        input.DropPressed -= OnDropPressed;
        LootBag.Changed -= Refresh;
        near = null;
        Refresh();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out VanStash stash)) return;
        near = stash;
        Refresh();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out VanStash stash)) return;
        near = null;
        Refresh();
    }

    private void Refresh()
    {
        bool bagHasItems = LootBag.Active != null && LootBag.Active.Count > 0;
        VanStash next = bagHasItems ? near : null;
        if (next == shown) return;

        if (shown != null && shown.Prompt != null) shown.Prompt.Hide();
        shown = next;
        if (shown != null && shown.Prompt != null) shown.Prompt.Show(transform, shown.PromptPoint, shown.UseText, false);
    }

    private void OnDropPressed()
    {
        if (shown != null) shown.Stash();
    }
}
