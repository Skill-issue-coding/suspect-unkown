using UnityEngine;

// The civilians' animator parameters, shared by CivilianLocomotionBuilder (which creates them) and the
// civilian animation component (which sets them from CivilianBehaviour). Speed and TurnRate come from
// NpcAnimatorParams, set by NpcAnimator. Rename here, rebuild the controller, and both sides follow
public static class CivilianAnimatorParams
{
    // The spot's CivilianStation as a float: the Dwell tree's blend parameter, one clip per whole number.
    // A float because blend trees take no ints
    public const string Station = "Station";

    // True while standing at a spot: the base layer leaves the walk for the Dwell tree
    public const string Dwelling = "Dwelling";

    public static readonly int StationId = Animator.StringToHash(Station);
    public static readonly int DwellingId = Animator.StringToHash(Dwelling);
}
