using System.Collections.Generic;
using UnityEngine;

// Lets the player use doors: tracks the reaches (one per side of a door) the player stands in, shows
// the prompt and outline for the nearest one, and on Interact opens the door or starts lockpicking.
// Trigger events arrive through the CharacterController (see PlayerConduct). An opening door switches
// both its reaches off, which sends no OnTriggerExit, so they are dropped on its Opened event instead;
// when it closes again, the re-enabled reaches send Enter
[RequireComponent(typeof(PlayerInputs))]
[RequireComponent(typeof(PlayerSkills))]
[RequireComponent(typeof(PlayerLockpicking))]
public class PlayerDoorInteraction : MonoBehaviour
{
    [SerializeField, Tooltip("Text in the prompt box. A door above the player's skill shows it greyed out, with a lock")]
    private string useText = "E";

    private readonly Dictionary<DoorReach, int> reachCounts = new();
    private readonly List<DoorReach> forgetBuffer = new();
    private DoorReach shown;

    private PlayerInputs input;
    private PlayerSkills skills;
    private PlayerLockpicking lockpicking;

    private void Awake()
    {
        input = GetComponent<PlayerInputs>();
        skills = GetComponent<PlayerSkills>();
        lockpicking = GetComponent<PlayerLockpicking>();
    }

    private void OnEnable()
    {
        if (input != null) input.InteractPressed += OnInteractPressed;
        if (lockpicking != null) lockpicking.Ended += Refresh;
    }

    private void OnDisable()
    {
        if (input != null) input.InteractPressed -= OnInteractPressed;
        if (lockpicking != null) lockpicking.Ended -= Refresh;
        SetShown(null, false);
    }

    private void OnInteractPressed()
    {
        if (lockpicking.IsPicking)
        {
            lockpicking.Cancel();
            return;
        }

        DoorReach reach = Nearest();
        if (reach == null) return;

        Door door = reach.Door;
        if (!door.IsLocked) door.Open();
        else if (CanPick(door)) lockpicking.Begin(door);

        Refresh();
    }

    private bool CanPick(Door door) => door.LockLevel <= skills.LockpickingLevel;

    // Only on trigger and lockpicking events, not per frame. Between two doors the prompt can
    // point at the one that was nearest at the last event; the press itself always takes the nearest
    private void Refresh()
    {
        DoorReach reach = Nearest();
        if (reach == null || lockpicking.IsPicking)
        {
            SetShown(null, false);
            return;
        }

        Door door = reach.Door;
        // Above the player's skill: the prompt shows a lock instead of explaining in text
        bool blocked = door.IsLocked && !CanPick(door);
        SetShown(reach, blocked);
    }

    // Show is called again even for the same reach, since blocked can change. Moving to the other side
    // of the same door turns its outline off and straight back on, which is harmless
    private void SetShown(DoorReach reach, bool blocked)
    {
        if (shown != null && shown != reach)
        {
            if (shown.Prompt != null) shown.Prompt.Hide();
            SetOutline(shown.Door, false);
        }

        if (reach != null)
        {
            if (reach.Prompt != null) reach.Prompt.Show(transform, reach.Door.PromptPoint, useText, blocked);
            if (reach != shown) SetOutline(reach.Door, true);
        }

        shown = reach;
    }

    // Only when the shown reach changes, never per frame
    private static void SetOutline(Door door, bool visible)
    {
        if (door == null) return;
        InteractOutline outline = door.GetComponentInChildren<InteractOutline>();
        if (outline != null) outline.SetVisible(visible);
    }

    private DoorReach Nearest()
    {
        DoorReach nearest = null;
        float nearestSqr = float.PositiveInfinity;
        Vector3 position = transform.position;

        foreach (DoorReach reach in reachCounts.Keys)
        {
            float sqr = (reach.transform.position - position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = reach;
            }
        }
        return nearest;
    }

    private void OnTriggerEnter(Collider other)
    {
        DoorReach reach = other.GetComponent<DoorReach>();
        if (reach == null || reach.Door == null || !reach.Door.enabled) return;

        if (!reachCounts.TryGetValue(reach, out int count))
            reach.Door.Opened += OnDoorOpened;

        reachCounts[reach] = count + 1;
        Refresh();
    }

    private void OnTriggerExit(Collider other)
    {
        DoorReach reach = other.GetComponent<DoorReach>();
        if (reach == null || !reachCounts.TryGetValue(reach, out int count)) return;

        if (count <= 1) Forget(reach);
        else reachCounts[reach] = count - 1;
        Refresh();
    }

    // Both reaches go at once. Collected first: a dictionary cannot change while it is being looped over
    private void OnDoorOpened(Door door)
    {
        forgetBuffer.Clear();
        foreach (DoorReach reach in reachCounts.Keys)
            if (reach.Door == door) forgetBuffer.Add(reach);

        foreach (DoorReach reach in forgetBuffer)
            Forget(reach);

        Refresh();
    }

    private void Forget(DoorReach reach)
    {
        reach.Door.Opened -= OnDoorOpened;
        reachCounts.Remove(reach);
    }
}
