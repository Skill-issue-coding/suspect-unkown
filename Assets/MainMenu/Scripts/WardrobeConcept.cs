using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The wardrobe station: arrows per outfit slot, the police profile beside it, the identity reset, and buying
// outfit parts. Parts with an OutfitPart price can be previewed with the arrows; their row shows the price,
// and the buy button pays for every unowned part of the shown outfit at once. Leaving the wardrobe puts the
// last owned outfit back on, so a preview is never carried into a job
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

    [Header("Outfit Purchase")]
    [Tooltip("Shows the player's clean money on the wardrobe canvas, above the buy button")]
    [SerializeField] private TMP_Text outfitMoneyLabel;
    [Tooltip("Buys every part of the shown outfit the player does not own yet")]
    [SerializeField] private Button buyButton;
    [Tooltip("Text on the buy button: the total price, or OWNED when there is nothing to buy")]
    [SerializeField] private TMP_Text buyLabel;
    [Tooltip("The menu, so the last owned outfit goes back on when the player leaves the wardrobe")]
    [SerializeField] private MainMenuConcept menu;

    [Header("Audio")]
    [Tooltip("Plays the purchase sound")]
    [SerializeField] private AudioSource sfxSource;
    [Tooltip("Played when a reset or an outfit is bought")]
    [SerializeField] private AudioClip purchaseSound;

    private WitnessReport record;
    private MenuButtonFx resetFx;
    private MenuButtonFx buyFx;

    // Names and total price of the shown parts the player does not own, rebuilt by Refresh
    private readonly List<string> unowned = new List<string>();
    private int unownedTotal;

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

        if (buyButton != null)
        {
            buyButton.onClick.AddListener(BuyOutfit);
            buyFx = buyButton.GetComponent<MenuButtonFx>();
        }

        // Load directly from police.json
        record = PoliceStore.Load();
        Refresh();
        RefreshMoney();
    }

    private void OnEnable()
    {
        if (wallet != null) wallet.Changed += RefreshMoney;
        if (menu != null) menu.StationShown += OnStationShown;
    }

    private void OnDisable()
    {
        if (wallet != null) wallet.Changed -= RefreshMoney;
        if (menu != null) menu.StationShown -= OnStationShown;
    }

    // Leaving the wardrobe while previewing a part not bought: put the saved, owned outfit back on
    private void OnStationShown(MenuStation station, float duration)
    {
        if (station == MenuStation.Wardrobe || unownedTotal == 0) return;

        customization.LoadAppearance();
        Refresh();
    }

    // Pays for every unowned part of the shown outfit at once, then saves the outfit, which is now owned
    public void BuyOutfit()
    {
        if (wallet == null || unownedTotal == 0 || !wallet.TrySpend(Wallet.Currency.Clean, unownedTotal)) return;

        wallet.OwnOutfitParts(unowned);
        customization.SaveAppearance();
        Refresh();
        if (sfxSource != null && purchaseSound != null)
            sfxSource.PlayOneShot(purchaseSound);
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
        string money = $"CLEAN MONEY  <color=#F2B33D>{Wallet.Money(wallet.Clean)}</color>";
        if (moneyLabel != null) moneyLabel.text = money;
        if (outfitMoneyLabel != null) outfitMoneyLabel.text = money;
        if (resetFx != null) resetFx.SetLocked(!wallet.CanAfford(Wallet.Currency.Clean, resetPrice));
        RefreshBuy();
    }

    // The buy button: the total for the unowned parts shown, red and locked when the player cannot afford it,
    // OWNED and locked when the whole outfit is owned
    private void RefreshBuy()
    {
        if (buyButton == null) return;

        bool affordable = wallet != null && wallet.CanAfford(Wallet.Currency.Clean, unownedTotal);
        if (buyLabel != null)
        {
            string total = Wallet.Money(unownedTotal);
            buyLabel.text = unownedTotal == 0 ? "OWNED"
                : affordable ? $"BUY  <color=#F2B33D>{total}</color>"
                : $"BUY  <color=#FF6B5B>{total}</color>";
        }

        bool canBuy = unownedTotal > 0 && affordable;
        if (buyFx != null) buyFx.SetLocked(!canBuy);
        else buyButton.interactable = canBuy;
    }

    private void Refresh()
    {
        if (record == null) record = PoliceStore.Load();

        // Read once per refresh (a click), never per frame
        ProgressSave save = ProgressStore.Load();
        unowned.Clear();
        unownedTotal = 0;

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

            // The price tag: a part not owned yet shows what it costs, and is added to the buy button's total
            string label = burned ? $"{Pretty(partName)}  <color=#FF6B5B>BURNED</color>" : Pretty(partName);
            if (part != null && !PlayerCustomization.IsOwned(part, save))
            {
                int price = PlayerCustomization.PriceOf(part);
                unowned.Add(partName);
                unownedTotal += price;
                label += $"  <color=#F2B33D>{Wallet.Money(price)}</color>";
            }
            row.partLabel.text = label;
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

        RefreshBuy();
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