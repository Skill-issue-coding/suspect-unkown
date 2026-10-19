using UnityEngine;

// The apartment clerk's animator parameters, shared by ClerkLocomotionBuilder (which creates them) and
// ResidentAnimation (which sets them from ResidentBehaviour). Speed, TurnRate and the phone pair come from
// NpcAnimatorParams. Rename here, rebuild the controller, and both sides follow
public static class ResidentAnimatorParams
{
    // ResidentPhase as an int: the base layer's states follow it
    public const string Phase = "Phase";

    // Paused by a scare: standing still and looking instead of snacking
    public const string Scared = "Scared";

    public static readonly int PhaseId = Animator.StringToHash(Phase);
    public static readonly int ScaredId = Animator.StringToHash(Scared);
}
