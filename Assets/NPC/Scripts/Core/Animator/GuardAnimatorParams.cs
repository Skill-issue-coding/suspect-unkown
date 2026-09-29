using UnityEngine;

// Parameter names shared by GuardLocomotionBuilder (which creates them) and the runtime scripts
// (which set them). Rename here, rebuild the controller, and both sides follow.
public static class GuardAnimatorParams
{
    public const string Watching = "Watching";
    public const string Searching = "Searching";
    public const string GiveUp = "GiveUp";

    public static readonly int WatchingId = Animator.StringToHash(Watching);
    public static readonly int SearchingId = Animator.StringToHash(Searching);
    public static readonly int GiveUpId = Animator.StringToHash(GiveUp);
}
