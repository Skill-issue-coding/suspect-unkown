using System;
using UnityEngine;

// The level's required loot: which items must be taken before the getaway van will leave, and how
// many are taken so far. Hears every pickup through LootPickup.Taken, so pickups need no reference
// to it. One per level. A level with nothing required is complete from the start
public class JobObjectives : MonoBehaviour
{
    [SerializeField, Tooltip("Items that must be taken before the van leaves. List an item twice to require two of it")]
    private LootItem[] required;

    private bool[] taken;

    public event Action Changed;

    public int Collected { get; private set; }
    public int Required => required.Length;
    public bool IsComplete => Collected >= Required;

    // One slot of the required list, for the job card's checklist. Index from 0 to Required - 1
    public LootItem RequiredItem(int index) => required[index];
    public bool IsTaken(int index) => taken[index];

    private void Awake() => taken = new bool[required.Length];

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
    }

    private void OnValidate()
    {
        if (required == null) return;
        foreach (LootItem item in required)
            if (item == null) { Debug.LogWarning($"{name} has an empty required item; the job can never complete", this); return; }
    }
}
