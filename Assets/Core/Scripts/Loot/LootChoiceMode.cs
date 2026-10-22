// How a LootChoice weighs its options
public enum LootChoiceMode
{
    Even,       // every option equally likely
    ByValue,    // chance falls with what the option is worth: weight 1 / value
    Weighted,   // each option's own weight
}
