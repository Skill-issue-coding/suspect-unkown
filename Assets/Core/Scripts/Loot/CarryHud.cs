using System.Collections.Generic;
using TMPro;
using UnityEngine;

// The carry panel's contents: the cash in the pockets as a running total, and the bag's slots as a tree
// under the Bag heading, one item per slot, "Empty" until filled. The bag holds two items in every job,
// so the slots are fixed lines in the prefab and the panel never changes size. Hears LootPickup.Taken,
// and changes only then. The bag only fills for now; once the van empties it (A7), the list moves into a
// carry model that both the van and this HUD read, and that model also refuses a third bag item
public class CarryHud : MonoBehaviour
{
    [SerializeField, Tooltip("Shows the pocket total")]
    private TMP_Text pocketLabel;

    [SerializeField, Tooltip("One label per bag slot, top to bottom")]
    private TMP_Text[] slotLabels;

    [SerializeField, Tooltip("Text of a slot with nothing in it")]
    private string emptyText = "Empty";

    private readonly List<LootItem> bagItems = new();
    private int pocket;

    private void Awake()
    {
        if (pocketLabel == null || slotLabels == null || slotLabels.Length == 0)
        {
            Debug.LogWarning($"{name} needs a pocket label and its slot labels; assign them in JobHud.prefab", this);
            enabled = false;
        }
    }

    private void OnEnable() => LootPickup.Taken += OnTaken;

    private void OnDisable() => LootPickup.Taken -= OnTaken;

    private void Start()
    {
        ShowPocket();
        ShowBag();
    }

    private void OnTaken(LootPickup pickup)
    {
        LootItem item = pickup.Item;
        if (item.CarriedIn == LootCarry.Pocket)
        {
            pocket += item.Value;
            ShowPocket();
            return;
        }

        bagItems.Add(item);
        // Nothing stops a third bag item yet; the carry model will. Until then the HUD says so
        if (bagItems.Count > slotLabels.Length)
            Debug.LogWarning($"{name}: {item.DisplayName} taken with the bag already full", this);
        ShowBag();
    }

    private void ShowPocket() => pocketLabel.text = $"${pocket}";

    private void ShowBag()
    {
        for (int i = 0; i < slotLabels.Length; i++)
            slotLabels[i].text = i < bagItems.Count ? bagItems[i].DisplayName : emptyText;
    }
}
