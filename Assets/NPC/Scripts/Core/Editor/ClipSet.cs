using System.Linq;
using UnityEngine;

// Interchangeable clips for one stance. One clip plays as is; several become a 1D tree on
// VariantParameter with clip n at threshold n, and the runtime sets the parameter to a whole number.
// Missing clips are dropped here, so every set holds only real clips.
public readonly struct ClipSet
{
    public readonly string Name;
    public readonly string VariantParameter;
    public readonly AnimationClip[] Clips;

    public ClipSet(string name, string variantParameter, params AnimationClip[] clips)
    {
        Name = name;
        VariantParameter = variantParameter;
        Clips = clips.Where(c => c != null).ToArray();
    }

    public bool IsEmpty => Clips.Length == 0;
    public bool HasVariants => Clips.Length > 1;
}