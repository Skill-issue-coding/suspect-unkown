using System;
using UnityEngine;

// Which body regions this observer attends to. 0 means no rays are cast at that region. 
// Separate from FeatureVisibility (M7): this decides what draws the eye, that decides 
// what the witness remembers.
[Serializable]
public struct RegionWeights
{
    [Range(0f, 1f)] public float head;
    [Range(0f, 1f)] public float upperBody;
    [Range(0f, 1f)] public float hands;
    [Range(0f, 1f)] public float lowerBody;
    [Range(0f, 1f)] public float feet;

    public RegionWeights(float head, float upperBody, float hands, float lowerBody, float feet)
    {
        this.head = head;
        this.upperBody = upperBody;
        this.hands = hands;
        this.lowerBody = lowerBody;
        this.feet = feet;
    }

    public readonly float Get(BodyRegion region) => region switch
    {
        BodyRegion.Head => head,
        BodyRegion.UpperBody => upperBody,
        BodyRegion.Hands => hands,
        BodyRegion.LowerBody => lowerBody,
        BodyRegion.Feet => feet,
        _ => throw new ArgumentOutOfRangeException(nameof(region))
    };

    public readonly bool AllZero => head == 0 && upperBody == 0 && hands == 0 && lowerBody == 0 && feet == 0;
}

