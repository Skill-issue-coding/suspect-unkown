using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// One buyable row or skill node in the concept shop. Knows its price and prerequisite,
// buys itself from the wallet when clicked, and redraws when the wallet changes.
// Hovering shows its description in the shop's detail line.
[RequireComponent(typeof(Button))]
public class ShopItem : MonoBehaviour, IPointerEnterHandler
{
    [Tooltip("Shown in the detail line on hover")]
    [SerializeField] private string title = "Item";
    [TextArea]
    [Tooltip("Shown in the detail line on hover")]
    [SerializeField] private string description = "";
    [Tooltip("Flag saved to progress.json when owned")]
    [SerializeField] private ProgressUnlock unlock = ProgressUnlock.None;
    [Tooltip("Price in the chosen currency")]
    [SerializeField] private int price = 500;
    [Tooltip("What the price is paid in")]
    [SerializeField] private Wallet.Currency currency = Wallet.Currency.Clean;
    [Tooltip("Must be owned before this can be bought. Empty for none")]
    [SerializeField] private ShopItem requires;
    [Tooltip("Owned from the start")]
    [SerializeField] private bool owned;
    [Tooltip("If above zero, buying this raises the fence rate to this value")]
    [SerializeField] private float fenceRate;
    [Tooltip("Not for sale yet: shows COMING SOON, looks locked and cannot be bought")]
    [SerializeField] private bool comingSoon;

    [Header("Presentation")]
    [SerializeField] private Wallet wallet;
    [SerializeField] private ShopConcept shop;
    [Tooltip("Price or state label")]
    [SerializeField] private TMP_Text priceLabel;
    [Tooltip("Border tinted by state")]
    [SerializeField] private Image border;

    private Button button;

    public bool Owned => owned;
    public string Title => title;
    public string Description => description;
    public ShopItem Requires => requires;
    public bool IsLocked => requires != null && !requires.Owned;
    public bool IsComingSoon => comingSoon;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (wallet == null)
        {
            Debug.LogWarning($"{name} has no wallet assigned", this);
            enabled = false;
            return;
        }
        button.onClick.AddListener(Buy);
    }

    private void OnEnable()
    {
        if (wallet == null) return;

        // sync ownership to progress.json
        if (wallet.HasUnlock(unlock))
        {
            owned = true;
        }
        else if (owned && unlock != ProgressUnlock.None)
        {
            // Starter items
            wallet.Unlock(unlock);
        }

        if (owned && fenceRate > 0f)
        {
            wallet.ImproveFence(fenceRate);
        }

        wallet.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (wallet != null) wallet.Changed -= Refresh;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (shop != null) shop.ShowDetail(this);
    }

    private void Buy()
    {
        if (owned || comingSoon || IsLocked || !wallet.CanAfford(currency, price)) return;

        // Owned before paying: TrySpend raises the wallet's Changed, and items that require this one
        // refresh their LOCKED state from it, also when this item has no unlock flag to save
        owned = true;
        wallet.TrySpend(currency, price);
        // Saved now, not on the next OnEnable: a job started straight after buying must already have it
        wallet.Unlock(unlock);
        if (fenceRate > 0f) wallet.ImproveFence(fenceRate);
        Refresh();
        if (shop != null)
        {
            shop.ShowDetail(this);
            shop.PlayPurchase();
        }
    }

    // Also called by the wallet event, so a purchase elsewhere unlocks dependants here
    private void Refresh()
    {
        string cost = currency == Wallet.Currency.Clean ? Wallet.Money(price) : $"{price} SP";
        Color tint;
        // Coming soon wins over everything, owned included: the item does not exist in the game yet
        if (comingSoon) { priceLabel.text = "<color=#7A7A7A>COMING SOON</color>"; tint = new Color(1f, 1f, 1f, 0.12f); }
        else if (owned) { priceLabel.text = "<color=#8FD18B>OWNED</color>"; tint = new Color(0.56f, 0.82f, 0.55f, 0.9f); }
        else if (IsLocked) { priceLabel.text = $"<color=#7A7A7A>LOCKED</color>"; tint = new Color(1f, 1f, 1f, 0.12f); }
        else if (!wallet.CanAfford(currency, price)) { priceLabel.text = $"<color=#FF6B5B>{cost}</color>"; tint = new Color(1f, 1f, 1f, 0.28f); }
        else { priceLabel.text = $"<color=#F2B33D>{cost}</color>"; tint = new Color(0.95f, 0.7f, 0.24f, 0.75f); }
        if (border != null) border.color = tint;
        button.interactable = !owned && !IsLocked && !comingSoon;
    }
}
