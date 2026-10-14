using UnityEngine;

// The apartment clerk's animator parameters, shared by ClerkLocomotionBuilder (which creates them) and
// ResidentAnimation (which sets them from ResidentBehaviour). Speed and TurnRate come from
// NpcAnimatorParams, set by NpcAnimator. Rename here, rebuild the controller, and both sides follow
public static class ResidentAnimatorParams
{
    // ResidentPhase as an int: the base layer's states follow it
    public const string Phase = "Phase";

    // Paused by a scare: standing still and looking instead of snacking
    public const string Scared = "Scared";

    // The upper-body layer: phone out while deciding, phone at the ear while calling
    public const string Deliberating = "Deliberating";
    public const string Calling = "Calling";

    public static readonly int PhaseId = Animator.StringToHash(Phase);
    public static readonly int ScaredId = Animator.StringToHash(Scared);
    public static readonly int DeliberatingId = Animator.StringToHash(Deliberating);
    public static readonly int CallingId = Animator.StringToHash(Calling);
}
