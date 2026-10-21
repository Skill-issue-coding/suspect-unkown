using UnityEngine;

// Guard-only animator parameters, shared by GuardLocomotionBuilder (which creates them) and
// GuardAnimation (which sets them). Rename here, rebuild the controller, and both sides follow.
public static class GuardAnimatorParams
{
    public const string Watching = "Watching";
    public const string Searching = "Searching";
    public const string GiveUp = "GiveUp";
    public const string Slam = "Slam";

    // Variant pickers: floats set to a whole number, never damped, so exactly one clip plays.
    public const string IdleVariant = "IdleVariant";
    public const string GiveUpVariant = "GiveUpVariant";

    // Clip counts per variant group. GuardLocomotionBuilder warns when GuardClips disagrees, and
    // GuardAnimation picks within these ranges.
    public const int IdleVariantCount = 2;
    public const int GiveUpVariantCount = 2;

    public static readonly int WatchingId = Animator.StringToHash(Watching);
    public static readonly int SearchingId = Animator.StringToHash(Searching);
    public static readonly int GiveUpId = Animator.StringToHash(GiveUp);
    public static readonly int SlamId = Animator.StringToHash(Slam);
    public static readonly int IdleVariantId = Animator.StringToHash(IdleVariant);
    public static readonly int GiveUpVariantId = Animator.StringToHash(GiveUpVariant);
}
