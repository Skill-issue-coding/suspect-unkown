using System;
using System.Collections.Generic;
using UnityEngine;

// The job's bag: which bag items the player carries, and how many fit. Pocket loot never takes a slot.
// One per level, on Job. Static Active and Changed, so pickups and the HUD (a prefab, which cannot
// reference a scene object) reach it with no lookup and no Awake-order trap
public class LootBag : MonoBehaviour
{
    [SerializeField, Min(1), Tooltip("Bag items the player can carry at once")]
    private int capacity = 2;

    private readonly List<LootItem> items = new();

    public static LootBag Active { get; private set; }
    public static event Action Changed;

    public int Capacity => capacity;
    public int Count => items.Count;
    public bool IsFull => items.Count >= capacity;
    public LootItem Item(int index) => items[index];

    private void OnEnable()
    {
        Active = this;
        LootPickup.Taken += OnTaken;
    }

    // On a scene reload the old bag can be disabled after the new one is enabled; only the bag that is
    // still Active clears it
    private void OnDisable()
    {
        LootPickup.Taken -= OnTaken;
        if (Active == this) Active = null;
    }

    private void OnTaken(LootPickup loot)
    {
        if (loot.Item.CarriedIn == LootCarry.Pocket) return;

        // The prompt refuses a full bag, so only a context menu Take gets here
        if (IsFull)
        {
            Debug.LogWarning($"{name}: {loot.Item.DisplayName} taken with the bag already full", this);
            return;
        }

        items.Add(loot.Item);
        Changed?.Invoke();
    }

    // Empties the bag into the van. The items stay counted: objectives and rewards count on take, not here
    [ContextMenu("Stash")]
    public int Stash()
    {
        int count = items.Count;
        if (count == 0) return 0;

        items.Clear();
        Changed?.Invoke();
        return count;
    }
}
