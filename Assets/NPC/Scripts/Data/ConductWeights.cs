using System;
using UnityEngine;

// How abnormal each conduct flag looks to one observer, 0 to 1.
// Abnormality combines set flags as 1 − Π(1 − w): never above 1, 
// and one more act never lowers it. One field and one switch arm 
// per ConductFlags value.
[Serializable]
public struct ConductWeights
{
    private static readonly ConductFlags[] AllFlags = (ConductFlags[])Enum.GetValues(typeof(ConductFlags));
    [Range(0f, 1f)] public float sprinting;
    [Range(0f, 1f)] public float inStaffArea;
    [Range(0f, 1f)] public float lockpicking;
    [Range(0f, 1f)] public float stealing;
    [Range(0f, 1f)] public float glasscutting;

    public readonly float Get(ConductFlags flag) => flag switch
    {
        ConductFlags.Sprinting => sprinting,
        ConductFlags.InStaffArea => inStaffArea,
        ConductFlags.Lockpicking => lockpicking,
        ConductFlags.Stealing => stealing,
        ConductFlags.GlassCutting => glasscutting,
        _ => throw new ArgumentOutOfRangeException(nameof(flag))
    };


    public readonly float Abnormality(ConductFlags flags)
    {
        float normal = 1f;
        foreach (ConductFlags flag in AllFlags)
        {
            if (flag == ConductFlags.None) continue;
            if ((flags & flag) != 0)
                normal *= 1f - Get(flag);
        }
        return 1f - normal;
    }

    public ConductWeights(float sprinting, float inStaffArea, float lockpicking, float stealing, float glasscutting)
    {
        this.sprinting = sprinting;
        this.inStaffArea = inStaffArea;
        this.lockpicking = lockpicking;
        this.stealing = stealing;
        this.glasscutting = glasscutting;
    }
}