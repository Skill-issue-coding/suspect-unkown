using System;
using UnityEngine;

// The level's objective loot and when the getaway van will leave. By default every listed item must be
// taken (the apartment, where a level with nothing listed is complete from the start). With story items
// optional, the listed items are the story items, and taking any of them or any bag item lets the van
// leave (the thrift shop, as in Payday). Hears every pickup through LootPickup.Taken, so pickups need no
// reference to it. One per level
public class JobObjectives : MonoBehaviour
{
    [SerializeField, Tooltip("Items that must be taken before the van leaves. List an item twice to require two of it")]
    private LootItem[] required;

    [SerializeField, Tooltip("Off: every listed item must be taken (the apartment). On: listed items are optional story items, and the van leaves once any of them or any bag item is taken")]
    private bool storyItemsOptional;

    private bool bagItemTaken;

    private bool[] taken;
    private bool[] previouslyUnlocked;

    public event Action Changed;

    public int Collected { get; private set; }
    public int Required => required.Length;
    public bool IsComplete => storyItemsOptional ? Collected > 0 || bagItemTaken : Collected >= Required;
    public bool StoryItemsOptional => storyItemsOptional;

    // One slot of the required list, for the job card's checklist. Index from 0 to Required - 1
    public LootItem RequiredItem(int index) => required[index];
    public bool IsTaken(int index) => taken[index] || previouslyUnlocked[index];

    private void Awake()
    {
        taken = new bool[required.Length];
        previouslyUnlocked = new bool[required.Length];
        if (!storyItemsOptional) return;
        ProgressSave save = ProgressStore.Load();

        for (int i = 0; i < required.Length; i++)
        {
            if (required[i] != null && required[i].ProgressUnlock != ProgressUnlock.None && save.Has(required[i].ProgressUnlock))
            {
                previouslyUnlocked[i] = true;
            }
        }
    }
    private void OnEnable() => LootPickup.Taken += OnTaken;

    private void OnDisable() => LootPickup.Taken -= OnTaken;

    private void OnTaken(LootPickup pickup)
    {
        for (int i = 0; i < required.Length; i++)
        {
            if (taken[i] || required[i] != pickup.Item) continue;

            taken[i] = true;
            Collected++;
            Debug.Log($"Objectives {Collected}/{Required}{(IsComplete ? ", complete" : "")}");
            Changed?.Invoke();
            return;
        }

        if (!storyItemsOptional || bagItemTaken || pickup.Item.CarriedIn != LootCarry.Bag) return;

        bagItemTaken = true;
        Debug.Log("Objectives: a bag item is taken, the van will leave");
        Changed?.Invoke();
    }

    private void OnValidate()
    {
        if (required == null) return;
        foreach (LootItem item in required)
            if (item == null) { Debug.LogWarning($"{name} has an empty required item; the job can never complete", this); return; }
    }
}
