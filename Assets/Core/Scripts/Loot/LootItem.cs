using UnityEngine;

// One kind of stealable item, as data: its name and how long it takes to take. Every pickup of
// the item shares the asset, and objectives match items by reference. Tuning only, no runtime
// state (decision 1). What it unlocks joins in A6, with the save file that reads it
[CreateAssetMenu(fileName = "LootItem", menuName = "Suspect Unknown/Loot Item")]
public class LootItem : ScriptableObject
{
    [SerializeField, Tooltip("Shown to the player and in logs")]
    private string displayName;

    [SerializeField, Tooltip("Seconds of holding E to take it"), Min(0.1f)]
    private float takeSeconds = 1f;

    public string DisplayName => displayName;
    public float TakeSeconds => takeSeconds;
}
