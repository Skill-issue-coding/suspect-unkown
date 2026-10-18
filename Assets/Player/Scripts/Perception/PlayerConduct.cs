using System.Collections.Generic;
using UnityEngine;

// What the player is doing right now, as NPCs read it. Posture is PlayerStance; 
// this is conduct only. Staff-area context comes from Zone triggers, counted per 
// zone so multi-collider zones work. Trigger events arrive only through the 
// CharacterController: adding a Rigidbody to the player would make all 17 bone 
// triggers fire zone events too. A zone disabled while the player is inside never 
// sends OnTriggerExit; zones are static level geometry, so that is accepted.
// Lockpicking comes from PlayerLockpicking, Stealing from PlayerInteraction.
[RequireComponent(typeof(PlayerMotor))]
public class PlayerConduct : MonoBehaviour
{
    private readonly Dictionary<Zone, int> zoneCounts = new();

    private PlayerMotor motor;
    private PlayerLockpicking lockpicking;
    private PlayerInteraction interaction;
    private bool isStealing;

    public ConductFlags Current
    {
        get
        {
            ConductFlags flags = ConductFlags.None;
            if (motor.IsSprinting) flags |= ConductFlags.Sprinting;
            if (IsInStaffArea()) flags |= ConductFlags.InStaffArea;
            if (lockpicking != null && lockpicking.IsPicking) flags |= ConductFlags.Lockpicking;
            if (isStealing) flags |= ConductFlags.Stealing;
            return flags;
        }
    }

    private bool IsInStaffArea()
    {
        foreach (Zone zone in zoneCounts.Keys)
        {
            if (zone.Type == ZoneType.Staff)
                return true;
        }
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        Zone zone = other.GetComponentInParent<Zone>();
        if (zone == null) return;

        zoneCounts.TryGetValue(zone, out int count);
        zoneCounts[zone] = count + 1;
    }

    private void OnTriggerExit(Collider other)
    {
        Zone zone = other.GetComponentInParent<Zone>();
        if (zone == null || !zoneCounts.TryGetValue(zone, out int count)) return;

        if (count <= 1) zoneCounts.Remove(zone);
        else zoneCounts[zone] = count - 1;
    }

    private void OnEnable()
    {
        if (interaction != null)
        {
            interaction.HoldStarted += OnHoldStarted;
            interaction.HoldEnded += OnHoldEnded;
        }
    }

    private void OnDisable()
    {
        if (interaction != null)
        {
            interaction.HoldStarted -= OnHoldStarted;
            interaction.HoldEnded -= OnHoldEnded;
        }
        isStealing = false;
    }

    private void OnHoldStarted(Interactable interactable)
    {
        if (interactable != null && interactable.GetComponentInParent<LootPickup>() != null)
            isStealing = true;
    }

    private void OnHoldEnded(Interactable interactable)
    {
        isStealing = false;
    }

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        lockpicking = GetComponent<PlayerLockpicking>();
        interaction = GetComponent<PlayerInteraction>();
    }

}
