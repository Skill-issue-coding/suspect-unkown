// The player's outfit as a witness sees it: one OutfitFeature per FeatureSlot. A struct of four
// structs, no arrays, so it can be copied into every Observation (decision 14) and a sighting keeps
// what the player wore at that moment
public struct CharacterAppearance
{
    public OutfitFeature head;
    public OutfitFeature chest;
    public OutfitFeature legs;
    public OutfitFeature feet;

    public OutfitFeature this[FeatureSlot slot]
    {
        get => slot switch
        {
            FeatureSlot.Head => head,
            FeatureSlot.Chest => chest,
            FeatureSlot.Legs => legs,
            _ => feet,
        };
        set
        {
            switch (slot)
            {
                case FeatureSlot.Head: head = value; break;
                case FeatureSlot.Chest: chest = value; break;
                case FeatureSlot.Legs: legs = value; break;
                default: feet = value; break;
            }
        }
    }

    public override string ToString() => $"head {head}, chest {chest}, legs {legs}, feet {feet}";
}