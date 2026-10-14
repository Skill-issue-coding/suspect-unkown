using UnityEngine;

// One kind of stealable item, as data: its name, how long it takes to take, where it is carried, and
// what it is worth once the job succeeds (the unlocks and money ProgressSave receives). Every pickup of
// the item shares the asset, and objectives match items by reference. Tuning only, no runtime state
// (decision 1)
[CreateAssetMenu(fileName = "LootItem", menuName = "Suspect Unknown/Loot Item")]
public class LootItem : ScriptableObject
{
    [SerializeField, Tooltip("Shown to the player and in logs")]
    private string displayName;

    [SerializeField, Tooltip("Seconds of holding E to take it"), Min(0.1f)]
    private float takeSeconds = 1f;

    [SerializeField, Tooltip("Bag: loot that has to reach the van. Pocket: cash that rides along for free")]
    private LootCarry carriedIn = LootCarry.Bag;

    [SerializeField, Tooltip("What taking this unlocks once the job succeeds")]
    private ProgressUnlock progressUnlock = ProgressUnlock.None;

    [SerializeField, Min(0), Tooltip("Money added to the save if the job succeeds")]
    private int value = 0;

    public string DisplayName => displayName;
    public float TakeSeconds => takeSeconds;
    public LootCarry CarriedIn => carriedIn;
    public ProgressUnlock ProgressUnlock => progressUnlock;
    public int Value => value;
}
