using UnityEngine;

// Animator parameters every NPC shares, guards now and civilians from M6. Builders create them;
// NpcAnimator sets them from the NavMeshAgent, and the role's animation component sets the phone pair
// from NpcDecisionWindow. Role-only parameters live in their own class (GuardAnimatorParams).
public static class NpcAnimatorParams
{
    // Metres per second, matching the locomotion tree's thresholds.
    public const string Speed = "Speed";

    // Yaw in degrees per second, positive turning right.
    public const string TurnRate = "TurnRate";

    // The upper-body Phone layer (AnimatorBuildUtility.AddPhoneLayer): phone out while deciding, phone
    // at the ear while calling. Used by every NPC with a decision window
    public const string Deliberating = "Deliberating";
    public const string Calling = "Calling";

    public static readonly int SpeedId = Animator.StringToHash(Speed);
    public static readonly int TurnRateId = Animator.StringToHash(TurnRate);
    public static readonly int DeliberatingId = Animator.StringToHash(Deliberating);
    public static readonly int CallingId = Animator.StringToHash(Calling);
}
