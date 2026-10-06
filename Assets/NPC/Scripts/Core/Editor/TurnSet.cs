using UnityEngine;

// Turn-in-place clips. Both must keep Root Transform Rotation unbaked, so the builder can measure
// their rate and the code, not the clip, does the actual turning.
public readonly struct TurnSet
{
    public readonly AnimationClip Left;
    public readonly AnimationClip Right;
    public readonly float LeftRate;
    public readonly float RightRate;

    public TurnSet(AnimationClip left, AnimationClip right, float fallbackRate)
    {
        Left = left;
        Right = right;
        LeftRate = left != null ? AnimatorBuildUtility.MeasureTurnRate(left, fallbackRate) : fallbackRate;
        RightRate = right != null ? AnimatorBuildUtility.MeasureTurnRate(right, fallbackRate) : fallbackRate;
    }

    public bool IsComplete => Left != null && Right != null;
}
