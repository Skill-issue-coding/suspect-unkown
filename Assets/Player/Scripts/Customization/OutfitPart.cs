using UnityEngine;

// The wardrobe price of one outfit part, on the part's own GameObject in Player.prefab. A part without
// this component is free and worn from the start; a part with it must be bought before it can be worn.
// Bought parts are saved by name in progress.json (ProgressSave.ownedOutfitParts)
public class OutfitPart : MonoBehaviour
{
    [SerializeField, Min(0), Tooltip("Clean money, dollars. 0 makes the part free")]
    private int price = 1000;

    public int Price => price;
}
