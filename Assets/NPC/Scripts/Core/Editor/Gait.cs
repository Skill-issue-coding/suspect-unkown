using UnityEngine;

// One moving speed on the locomotion tree: a clip and its speed in metres per second,
// measured once when loaded.
public readonly struct Gait
{
    public readonly AnimationClip Clip;
    public readonly float Speed;

    public Gait(AnimationClip clip, float fallbackSpeed)
    {
        Clip = clip;
        Speed = clip != null ? AnimatorBuildUtility.MeasureSpeed(clip, fallbackSpeed) : fallbackSpeed;
    }

    public bool IsEmpty => Clip == null;
}
