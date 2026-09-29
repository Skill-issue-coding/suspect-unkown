using UnityEngine;

public static class NpcAnimatorParams
{
    public const string Speed = "Speed";
    public const string TurnRate = "TurnRate";

    public static readonly int SpeedId = Animator.StringToHash(Speed);
    public static readonly int TurnRateId = Animator.StringToHash(TurnRate);
}
