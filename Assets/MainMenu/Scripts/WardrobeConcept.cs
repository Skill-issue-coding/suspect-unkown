using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WardrobeConcept : MonoBehaviour
{
    [Serializable]
    public class Row
    {
        [Tooltip("PlayerCustomization category this row drives, for example Head")]
        public string category;
        [Tooltip("Matching FeatureSlot in the WitnessReport (Head, Chest, Legs, Feet)")]
        public FeatureSlot slot;
        [Tooltip("Arrow buttons")]
        public Button previous, next;
        [Tooltip("Equipped part name and its position in the list")]
        public TMP_Text partLabel, countLabel;
        [Tooltip("Multiline text on the police canvas for this category")]
        public TMP_Text profileLabel;
    }

    [Tooltip("The player in the wardrobe")]
    [SerializeField] private PlayerCustomization customization;
    [Tooltip("One row per category, top to bottom")]
    [SerializeField] private Row[] rows;
    [Tooltip("Shown in red at the bottom when equipped parts are on the police board")]
    [SerializeField] private TMP_Text warning;
    [Tooltip("Button to wipe police.json memory")]
    [SerializeField] private Button identityResetButton;

    [Header("Identity Reset Cost")]
    [Tooltip("Wallet the reset is paid from")]
    [SerializeField] private Wallet wallet;
    [Tooltip("Price of a reset in clean money, dollars")]
    [SerializeField] private int resetPrice = 12500;
    [Tooltip("Shows the player's clean money above the reset button")]
    [SerializeField] private TMP_Text moneyLabel;

    [Header("Audio")]
    [Tooltip("Plays the purchase sound")]
    [SerializeField] private AudioSource sfxSource;
    [Tooltip("Played when a reset is bought")]
    [SerializeField] private AudioClip purchaseSound;

    private WitnessReport record;
    private MenuButtonFx resetFx;

    private void Awake()
    {
        if (customization == null || rows == null || rows.Length == 0)
        {
            Debug.LogWarning($"{name} needs a PlayerCustomization and rows", this);
            enabled = false;
        }
    }

    private void Start()
    {
        foreach (Row row in rows)
        {
            Row r = row;
            r.previous.onClick.AddListener(() => { customization.PreviousPart(r.category); Refresh(); });
            r.next.onClick.AddListener(() => { customization.NextPart(r.category); Refresh(); });
        }

        if (identityResetButton != null)
        {
            identityResetButton.onClick.AddListener(ResetIdentity);
            resetFx = identityResetButton.GetComponent<MenuButtonFx>();
        }

        // Load directly from police.json
        record = PoliceStore.Load();
        Refresh();
        RefreshMoney();
    }

    private void OnEnable()
    {
        if (wallet != null) wallet.Changed += RefreshMoney;
    }

    private void OnDisable()
    {
        if (wallet != null) wallet.Changed -= RefreshMoney;
    }

    // Paid from clean money like a shop item: nothing is wiped unless the wallet takes the payment
    public void ResetIdentity()
    {
        if (wallet == null || !wallet.TrySpend(Wallet.Currency.Clean, resetPrice)) return;

        PoliceStore.Delete();
        record = PoliceStore.Load(); // Returns empty WitnessReport
        Refresh();
        if (sfxSource != null && purchaseSound != null)
            sfxSource.PlayOneShot(purchaseSound);
    }

    // Also called by the wallet event, so laundering or buying in the shop updates the label and the lock
    private void RefreshMoney()
    {
        if (wallet == null) return;
        if (moneyLabel != null)
            moneyLabel.text = $"CLEAN MONEY  <color=#F2B33D>{Wallet.Money(wallet.Clean)}</color>";
        if (resetFx != null) resetFx.SetLocked(!wallet.CanAfford(Wallet.Currency.Clean, resetPrice));
    }

    private void Refresh()
    {
        if (record == null) record = PoliceStore.Load();

        int burnedCount = 0;

        foreach (Row row in rows)
        {
            PlayerCustomization.CustomizationCategory category = Find(row.category);
            if (category == null || category.parts.Length == 0) continue;

            GameObject part = category.parts[category.currentIndex];
            string partName = part != null ? part.name : "";

            // Burned by the same rule recognition uses in a job (WitnessReport.IsBurned), so the wardrobe
            // never says one thing and the guards do another
            bool burned = OutfitFeature.TryParse(partName, out _, out OutfitFeature equipped) && record.IsBurned(row.slot, equipped);
            if (burned) burnedCount++;

            row.partLabel.text = burned ? $"{Pretty(partName)}  <color=#FF6B5B>BURNED</color>" : Pretty(partName);
            row.countLabel.text = $"{category.currentIndex + 1} / {category.parts.Length}";

            // What the police believe about this slot: the leading part, with color and style confidence apart,
            // as recognition reads them
            if (row.profileLabel != null)
            {
                row.profileLabel.text = record.TryDescribe(row.slot, out OutfitFeature known, out float styleConfidence, out float colorConfidence)
                    ? $"[Style {known.style} {known.color}]\nColor {Percent(colorConfidence)} - Style {Percent(styleConfidence)}"
                    : "[?]";
            }
        }

        // Red warning text at the bottom: burned pieces, and whether there are enough to be known on sight
        if (warning != null)
        {
            int toKnow = WitnessReport.BurnedPartsToKnow;
            warning.text = burnedCount == 0
                ? "<color=#8FD18B>No burned pieces in this outfit</color>"
                : burnedCount >= toKnow
                    ? $"<color=#FF6B5B>{burnedCount} burned pieces. You will be recognised on sight.</color>"
                    : $"<color=#FF6B5B>{burnedCount} burned piece{(burnedCount > 1 ? "s" : "")}. {toKnow - burnedCount} more and you will be recognised on sight.</color>";
        }
    }

    private PlayerCustomization.CustomizationCategory Find(string categoryName)
    {
        foreach (PlayerCustomization.CustomizationCategory category in customization.categories)
            if (category.categoryName == categoryName) return category;
        return null;
    }

    private static string Percent(float confidence) => $"{Mathf.RoundToInt(confidence * 100f)}%";

    // "Chest_002_Red" reads as "Style 2  Red"; anything else is shown as it is
    public static string Pretty(string partName)
    {
        string[] bits = partName.Split('_');
        if (bits.Length == 3 && int.TryParse(bits[1], out int style)) return $"Style {style}  {bits[2]}";
        return partName;
    }
}