using System;
using System.Collections.Generic;
using UnityEngine;

// Tracks the interactables the player can reach, shows the prompt and outline for
// the nearest, and handles press and hold input. Replaces PlayerLootInteraction and
// PlayerDoorInteraction.
[RequireComponent(typeof(PlayerInputs))]
public class PlayerInteraction : MonoBehaviour
{
    private readonly Dictionary<Interactable, int> reachCounts = new Dictionary<Interactable, int>();

    private Interactable shown;
    private float progress;
    private bool isHolding;
    private bool isBusy;
    private PlayerInputs input;
    private PlayerSkills skills;

    public bool IsBusy
    {
        get => isBusy;
        set
        {
            if (isBusy == value) return;
            isBusy = value;
            Refresh();
        }
    }

    public event Action<Interactable> HoldStarted;
    public event Action<Interactable> HoldEnded;

    private void Awake()
    {
        input = GetComponent<PlayerInputs>();
        skills = GetComponent<PlayerSkills>();
    }

    private void OnEnable()
    {
        if (input != null)
            input.InteractPressed += OnInteractPressed;
    }

    private void OnDisable()
    {
        if (input != null)
            input.InteractPressed -= OnInteractPressed;

        SetShown(null);

        foreach (Interactable interactable in reachCounts.Keys)
        {
            if (interactable != null)
                interactable.Withdrawn -= OnWithdrawn;
        }

        reachCounts.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        Interactable interactable = other.GetComponentInParent<Interactable>();
        if (interactable == null) return;

        if (reachCounts.TryGetValue(interactable, out int count))
        {
            reachCounts[interactable] = count + 1;
        }
        else
        {
            reachCounts[interactable] = 1;
            interactable.Withdrawn += OnWithdrawn;
        }

        Refresh();
    }

    private void OnTriggerExit(Collider other)
    {
        Interactable interactable = other.GetComponentInParent<Interactable>();
        if (interactable == null || !reachCounts.TryGetValue(interactable, out int count)) return;

        if (count <= 1)
            Forget(interactable);
        else
            reachCounts[interactable] = count - 1;

        Refresh();
    }

    private void OnWithdrawn(Interactable interactable)
    {
        Forget(interactable);
        Refresh();
    }

    private void Forget(Interactable interactable)
    {
        if (interactable == null) return;

        interactable.Withdrawn -= OnWithdrawn;
        reachCounts.Remove(interactable);
    }

    public void Refresh()
    {
        SetShown(IsBusy ? null : Nearest());
    }

    private Interactable Nearest()
    {
        Interactable nearest = null;
        float bestDistSq = float.PositiveInfinity;
        Vector3 playerPos = transform.position;

        foreach (Interactable candidate in reachCounts.Keys)
        {
            if (candidate == null) continue;

            float distSq = (candidate.transform.position - playerPos).sqrMagnitude;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private void SetShown(Interactable next)
    {
        if (shown == next) return;

        // If a hold was in progress on the previous interactable, end it cleanly first
        EndHold();

        if (shown != null)
        {
            if (shown.Prompt != null) shown.Prompt.Hide();
            if (shown.Outline != null) shown.Outline.SetVisible(false);
        }

        shown = next;
        progress = 0f;

        if (shown != null)
        {
            if (shown.Prompt != null) shown.Prompt.Show(transform, shown.PromptPoint, shown.UseText, shown.IsBlockedFor(gameObject));
            if (shown.Outline != null) shown.Outline.SetVisible(true);
        }
    }

    private void OnInteractPressed()
    {
        if (IsBusy || shown == null) return;

        // Checked before the hold test, so a refused hold is announced on the press as well
        if (shown.IsBlockedFor(gameObject))
        {
            shown.Refuse();
            return;
        }

        if (shown.HoldSeconds > 0f) return;
        shown.Use(gameObject);
    }

    private void Update()
    {
        if (IsBusy || shown == null || shown.HoldSeconds <= 0f) return;

        if (input.interact && !shown.IsBlockedFor(gameObject))
        {
            if (!isHolding)
            {
                isHolding = true;
                HoldStarted?.Invoke(shown);
            }
            float holdDuration = shown.HoldSeconds;
            if (skills != null && skills.HasQuickHands && shown.GetComponent<LootPickup>() != null)
            {
                holdDuration = Math.Min(holdDuration, 1.5f);
            }
            progress += Time.deltaTime / holdDuration;
            if (shown.Prompt != null)
                shown.Prompt.SetProgress(Mathf.Clamp01(progress));

            if (progress >= 1f)
            {
                Interactable completed = shown;
                EndHold();
                completed.Use(gameObject);
            }
        }
        else if (isHolding)
        {
            EndHold();
        }
    }

    private void EndHold()
    {
        if (!isHolding) return;

        isHolding = false;
        progress = 0f;

        if (shown != null)
        {
            if (shown.Prompt != null) shown.Prompt.SetProgress(0f);
            HoldEnded?.Invoke(shown);
        }
    }
}