using System;
using UnityEngine;

// The level's objective loot and when the getaway van will leave. Listed items appear on the HUD
// checklist card and count as primary objectives on the debrief screen. When canLeaveAnytime or
// storyItemsOptional is on, the getaway van is ready from the start (no loot required to leave).
// Hears every pickup through LootPickup.Taken, so pickups need no reference to it. One per level
public class JobObjectives : MonoBehaviour
{
    [SerializeField, Tooltip("Items listed on the HUD checklist and debrief. If storyItemsOptional and canLeaveAnytime are off, all must be taken to leave")]
    private LootItem[] required;

    [SerializeField, Tooltip("Off: all listed items must be taken (Apartment). On: items are optional story items, and the van can leave anytime(Thriftshop)")]
    private bool storyItemsOptional;

    [SerializeField, Tooltip("When enabled, the getaway van can leave at any time without collecting loot")]
    private bool canLeaveAnytime = true;

    private bool[] taken;
    private bool[] previouslyUnlocked;

    public event Action Changed;

    public int Collected { get; private set; }
    public int Required => required != null ? required.Length : 0;
    public bool IsComplete => canLeaveAnytime || storyItemsOptional || (Collected >= Required);
    public bool StoryItemsOptional => storyItemsOptional;

    // One slot of the required list, for the job card's checklist. Index from 0 to Required - 1
    public LootItem RequiredItem(int index) => required[index];
    public bool IsTaken(int index) => taken[index] || previouslyUnlocked[index];

    private void Awake()
    {
        taken = new bool[Required];
        previouslyUnlocked = new bool[Required];

        if (!storyItemsOptional) return;

        ProgressSave save = ProgressStore.Load();
        for (int i = 0; i < Required; i++)
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
        for (int i = 0; i < Required; i++)
        {
            if (taken[i] || required[i] != pickup.Item) continue;

            taken[i] = true;
            Collected++;
            Debug.Log($"Objectives {Collected}/{Required}{(IsComplete ? ", complete" : "")}");
            Changed?.Invoke();
            return;
        }
    }

    private void OnValidate()
    {
        if (required == null) return;
        foreach (LootItem item in required)
            if (item == null) { Debug.LogWarning($"{name} has an empty required item", this); return; }
    }
}