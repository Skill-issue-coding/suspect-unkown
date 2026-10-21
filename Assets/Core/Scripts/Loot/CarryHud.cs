using TMPro;
using UnityEngine;

// The carry panel's contents: the cash in the pockets as a running total, and the bag's slots as a tree
// under the Bag heading, one item per slot, "Empty" until filled. The bag holds two items in every job,
// so the slots are fixed lines in the prefab and the panel never changes size. The pocket total hears
// LootPickup.Taken; the slots read LootBag, the one owner of what is in the bag, on LootBag.Changed
public class CarryHud : MonoBehaviour
{
    [SerializeField, Tooltip("Shows the pocket total")]
    private TMP_Text pocketLabel;

    [SerializeField, Tooltip("One label per bag slot, top to bottom")]
    private TMP_Text[] slotLabels;

    [SerializeField, Tooltip("Text of a slot with nothing in it")]
    private string emptyText = "Empty";

    private int pocket;

    private void Awake()
    {
        if (pocketLabel == null || slotLabels == null || slotLabels.Length == 0)
        {
            Debug.LogWarning($"{name} needs a pocket label and its slot labels; assign them in JobHud.prefab", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        LootPickup.Taken += OnTaken;
        LootBag.Changed += ShowBag;
    }

    private void OnDisable()
    {
        LootPickup.Taken -= OnTaken;
        LootBag.Changed -= ShowBag;
    }

    // After every OnEnable, so the level's LootBag is Active by now
    private void Start()
    {
        ShowPocket();
        ShowBag();
    }

    private void OnTaken(LootPickup pickup)
    {
        LootItem item = pickup.Item;
        if (item.CarriedIn != LootCarry.Pocket) return;

        pocket += item.Value;
        ShowPocket();
    }

    private void ShowPocket() => pocketLabel.text = $"${pocket}";

    // No bag in the level: every slot shows empty
    private void ShowBag()
    {
        LootBag bag = LootBag.Active;
        int count = bag != null ? bag.Count : 0;
        for (int i = 0; i < slotLabels.Length; i++)
            slotLabels[i].text = i < count ? bag.Item(i).DisplayName : emptyText;
    }
}
