using System.Collections.Generic;
using UnityEngine;

// Lets the player take loot: tracks the pickups in reach, shows the prompt and outline for the
// nearest, and fills the prompt's ring while E is held. IsTaking (the Stealing conduct flag) is
// true only during the hold. A taken pickup switches off, which sends no OnTriggerExit, so it is
// dropped on LootPickup.Taken instead
[RequireComponent(typeof(PlayerInputs))]
public class PlayerLootInteraction : MonoBehaviour
{
    [SerializeField, Tooltip("Text in the prompt box")]
    private string useText = "E";

    private readonly Dictionary<LootPickup, int> reachCounts = new();
    private LootPickup shown;
    private float progress;

    private PlayerInputs input;
    private PlayerLockpicking lockpicking;

    public bool IsTaking { get; private set; }

    private void Awake()
    {
        input = GetComponent<PlayerInputs>();
        lockpicking = GetComponent<PlayerLockpicking>();
    }

    private void Update()
    {
        bool holding = shown != null && input.interact &&
                       (lockpicking == null || !lockpicking.IsPicking);
        IsTaking = holding;

        if (!holding)
        {
            if (progress > 0f)
            {
                progress = 0f;
                if (shown != null && shown.Prompt != null) shown.Prompt.SetProgress(0f);
            }
            return;
        }

        progress += Time.deltaTime / shown.Item.TakeSeconds;
        if (shown.Prompt != null) shown.Prompt.SetProgress(progress);

        if (progress >= 1f)
        {
            IsTaking = false;
            shown.Take();
        }
    }

    private void OnEnable() => LootPickup.Taken += OnTaken;

    private void OnDisable()
    {
        LootPickup.Taken -= OnTaken;
        SetShown(null);
        IsTaking = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        LootPickup pickup = other.GetComponent<LootPickup>();
        if (pickup == null || !pickup.enabled) return;

        reachCounts.TryGetValue(pickup, out int count);
        reachCounts[pickup] = count + 1;
        Refresh();
    }

    private void OnTriggerExit(Collider other)
    {
        LootPickup pickup = other.GetComponent<LootPickup>();
        if (pickup == null || !reachCounts.TryGetValue(pickup, out int count)) return;

        if (count <= 1) reachCounts.Remove(pickup);
        else reachCounts[pickup] = count - 1;
        Refresh();
    }

    private void OnTaken(LootPickup pickup)
    {
        if (!reachCounts.Remove(pickup)) return;
        Refresh();
    }

    private void Refresh() => SetShown(Nearest());

    private LootPickup Nearest()
    {
        LootPickup nearest = null;
        float nearestSqr = float.PositiveInfinity;
        Vector3 position = transform.position;

        foreach (LootPickup pickup in reachCounts.Keys)
        {
            float sqr = (pickup.transform.position - position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = pickup;
            }
        }
        return nearest;
    }

    private void SetShown(LootPickup pickup)
    {
        if (pickup == shown) return;

        if (shown != null)
        {
            if (shown.Prompt != null) shown.Prompt.Hide();
            if (shown.Outline != null) shown.Outline.SetVisible(false);
        }

        if (pickup != null)
        {
            if (pickup.Prompt != null) pickup.Prompt.Show(transform, pickup.PromptPoint, useText, false);
            if (pickup.Outline != null) pickup.Outline.SetVisible(true);
        }

        shown = pickup;
        progress = 0f;
    }
}
