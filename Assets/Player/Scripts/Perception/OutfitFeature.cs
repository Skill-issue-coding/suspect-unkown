using System;

// One slot of an outfit: its style number and its color, kept apart so a witness can remember one and
// get the other wrong
public struct OutfitFeature
{
    public int style;
    public AppearanceColor color;

    public OutfitFeature(int style, AppearanceColor color)
    {
        this.style = style;
        this.color = color;
    }

    // Reads a part name, Slot_Style_Color (Chest_002_Black). False for any other name
    public static bool TryParse(string partName, out FeatureSlot slot, out OutfitFeature feature)
    {
        feature = default;
        slot = default;
        string[] bits = partName.Split('_');
        if (bits.Length != 3 || !Enum.TryParse(bits[0], out slot) || !int.TryParse(bits[1], out int style) ||
            !Enum.TryParse(bits[2], out AppearanceColor color))
            return false;

        feature = new OutfitFeature(style, color);
        return true;
    }

    public override string ToString() => $"{color} {style:000}";
}
