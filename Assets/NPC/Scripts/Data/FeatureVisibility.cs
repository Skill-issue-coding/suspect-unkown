using System;
using UnityEngine;

// How much this observer notices each part of an outfit, 0 to 1, as a factor in evidence quality
// (concept 6.2, featureVisibility). Separate from RegionWeights: those decide what draws the eye, this
// decides what the witness remembers. A ceiling camera reads hats well and shoes badly
[Serializable]
public struct FeatureVisibility
{
    [Range(0f, 1f)] public float head;
    [Range(0f, 1f)] public float chest;
    [Range(0f, 1f)] public float legs;
    [Range(0f, 1f)] public float feet;

    public FeatureVisibility(float head, float chest, float legs, float feet)
    {
        this.head = head;
        this.chest = chest;
        this.legs = legs;
        this.feet = feet;
    }

    public readonly float Get(FeatureSlot slot) => slot switch
    {
        FeatureSlot.Head => head,
        FeatureSlot.Chest => chest,
        FeatureSlot.Legs => legs,
        FeatureSlot.Feet => feet,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public readonly bool AllZero => head == 0 && chest == 0 && legs == 0 && feet == 0;
}
