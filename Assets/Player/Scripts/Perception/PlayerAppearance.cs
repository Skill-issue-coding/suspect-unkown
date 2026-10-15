using System;
using UnityEngine;

// What the player is wearing right now, as data: reads PlayerCustomization's active parts into a
// CharacterAppearance whenever the outfit changes. Part names must be Slot_Style_Color
// (Chest_002_Black); a name that does not parse is warned about and read as style 0, Unknown.
// On the player root, beside PerceptionTargets, so NPCs find it with the other things they read
public class PlayerAppearance : MonoBehaviour
{
    [SerializeField, Tooltip("Log the outfit whenever it changes; the W1 check")]
    private bool logChanges = true;

    private PlayerCustomization customization;

    public CharacterAppearance Current { get; private set; }

    private void Awake()
    {
        customization = GetComponentInChildren<PlayerCustomization>();
        if (customization == null)
        {
            Debug.LogWarning($"{name} found no PlayerCustomization below it; PlayerAppearance disabled", this);
            enabled = false;
        }
    }

    private void OnEnable() { if (customization != null) customization.Changed += Read; }

    private void OnDisable() { if (customization != null) customization.Changed -= Read; }

    private void Start() => Read();

    private void Read()
    {
        CharacterAppearance appearance = default;

        foreach (PlayerCustomization.CustomizationCategory category in customization.categories)
        {
            if (!Enum.TryParse(category.categoryName, out FeatureSlot slot)) continue;
            // A look saved before parts were removed can point past the end
            if (category.currentIndex < 0 || category.currentIndex >= category.parts.Length) continue;
            GameObject part = category.parts[category.currentIndex];
            if (part != null) appearance[slot] = Parse(part.name);
        }

        Current = appearance;
        if (logChanges) Debug.Log($"Appearance: {Current}", this);
    }

    private OutfitFeature Parse(string partName)
    {
        if (OutfitFeature.TryParse(partName, out _, out OutfitFeature feature)) return feature;

        Debug.LogWarning($"{name}: part {partName} is not named Slot_Style_Color; read as Unknown", this);
        return new OutfitFeature(0, AppearanceColor.Unknown);
    }
}
