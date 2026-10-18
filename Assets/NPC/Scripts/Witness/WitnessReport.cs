using System;
using System.Text;
using UnityEngine;
using System.Collections.Generic;

// One description of the player's appearance: per outfit slot, log-odds beliefs about style and color.
// NpcWitness keeps a live one as its memory and hands out a copy at job end; JobWitnesses merges the copies
// into the job's police description. Plain serializable values, so it can be saved as it is (W8)

[Serializable]
public class WitnessReport
{
    // How many values each property can take, so beliefs start even. A style above StyleCount is ignored
    // until this grows
    public const int StyleCount = 3;
    public static readonly int ColorCount = Enum.GetValues(typeof(AppearanceColor)).Length - 1; // minus Unknown

    private static readonly FeatureSlot[] Slots = (FeatureSlot[])Enum.GetValues(typeof(FeatureSlot));

    [SerializeField] private FeatureBelief[] styles;
    [SerializeField] private FeatureBelief[] colors;

    public WitnessReport()
    {
        styles = new FeatureBelief[Slots.Length];
        colors = new FeatureBelief[Slots.Length];
        for (int i = 0; i < Slots.Length; i++)
        {
            styles[i] = new FeatureBelief(StyleCount);
            colors[i] = new FeatureBelief(ColorCount);
        }
    }

    // False for a witness who never learned anything: "No witnesses came forward"
    public bool SawAnything
    {
        get
        {
            for (int i = 0; i < Slots.Length; i++)
                if (styles[i].Learned || colors[i].Learned) return true;
            return false;
        }
    }

    public void AddEvidence(FeatureSlot slot, OutfitFeature seen, float weight)
    {
        styles[(int)slot].AddEvidence(seen.style, weight);
        colors[(int)slot].AddEvidence((int)seen.color, weight);
    }

    public void Fade(float factor)
    {
        for (int i = 0; i < Slots.Length; i++)
        {
            styles[i].Fade(factor);
            colors[i].Fade(factor);
        }
    }

    // Adds another witness's evidence, slot by slot
    public void Merge(WitnessReport other)
    {
        for (int i = 0; i < Slots.Length; i++)
        {
            styles[i].Merge(other.styles[i]);
            colors[i].Merge(other.colors[i]);
        }
    }

    public WitnessReport Copy()
    {
        WitnessReport copy = new WitnessReport();
        for (int i = 0; i < Slots.Length; i++)
        {
            copy.styles[i] = styles[i].Copy();
            copy.colors[i] = colors[i].Copy();
        }
        return copy;
    }

    // False for a report read from a file that does not match today's slots, styles and colors
    public bool IsWellFormed()
    {
        if (styles == null || colors == null || styles.Length != Slots.Length || colors.Length != Slots.Length) return false;
        for (int i = 0; i < Slots.Length; i++)
        {
            if (styles[i] == null || !styles[i].Fits(StyleCount)) return false;
            if (colors[i] == null || !colors[i].Fits(ColorCount)) return false;
        }
        return true;
    }

    // The leading style and color for one slot, and how sure of each. False while nothing is known about it
    public bool TryDescribe(FeatureSlot slot, out OutfitFeature feature, out float styleConfidence, out float colorConfidence)
    {
        int i = (int)slot;
        bool hasStyle = styles[i].TryGetLeading(out int style, out styleConfidence);
        bool hasColor = colors[i].TryGetLeading(out int color, out colorConfidence);
        feature = new OutfitFeature(style, (AppearanceColor)color);
        return hasStyle || hasColor;
    }

    // One line per slot, for logs and the debug readout
    public override string ToString()
    {
        StringBuilder text = new StringBuilder();
        foreach (FeatureSlot slot in Slots)
        {
            if (TryDescribe(slot, out OutfitFeature f, out float sc, out float cc))
                text.AppendLine($"{slot} {f.color} {cc:P0} · style {f.style:000} {sc:P0}");
            else
                text.AppendLine($"{slot} —");
        }
        return text.ToString();
    }

    public List<(OutfitFeature feature, float confidence)> DescribeTop(FeatureSlot slot, int maxCount = 3, float minScore = 0.05f)
    {
        int i = (int)slot;
        var topStyles = styles[i].GetRanked(minScore);
        var topColors = colors[i].GetRanked(minScore);

        int count = Mathf.Min(maxCount, Mathf.Min(topStyles.Count, topColors.Count));
        List<(OutfitFeature feature, float confidence)> results = new(count);

        for (int k = 0; k < count; k++)
        {
            OutfitFeature feature = new OutfitFeature(topStyles[k].value, (AppearanceColor)topColors[k].value);
            float combinedConfidence = (topStyles[k].confidence + topColors[k].confidence) * 0.5f;
            results.Add((feature, combinedConfidence));
        }

        return results;
    }
}
