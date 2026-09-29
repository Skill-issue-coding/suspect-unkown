using UnityEngine;

// Animator parameters every NPC shares, guards now and civilians from M6. Builders create them;
// NpcAnimator sets them from the NavMeshAgent. Role-only parameters live in their own class
// (GuardAnimatorParams).
public static class NpcAnimatorParams
{
    // Metres per second, matching the locomotion tree's thresholds.
    public const string Speed = "Speed";

    // Yaw in degrees per second, positive turning right.
    public const string TurnRate = "TurnRate";

    public static readonly int SpeedId = Animator.StringToHash(Speed);
    public static readonly int TurnRateId = Animator.StringToHash(TurnRate);
}
