using UnityEngine;
using TMPro;

// Concept shop at the workbench laptop: tabs for skills (concept 7.4), device abilities
// (6.7, decision 20), gear (7.3) and safehouse upgrades (7.5), plus the fence that
// launders dirty money. Owns tab switching and the hover detail line; each ShopItem
// handles its own purchase.
public class ShopConcept : MonoBehaviour
{
    [Tooltip("Tab buttons, same order as the pages")]
    [SerializeField] private MenuButtonFx[] tabs;
    [Tooltip("Tab pages, same order as the tab buttons")]
    [SerializeField] private GameObject[] pages;
    [Tooltip("Detail line at the bottom of the panel")]
    [SerializeField] private TMP_Text detailTitle, detailBody;
    [Tooltip("Wallet the launder button converts")]
    [SerializeField] private Wallet wallet;
    [Tooltip("Launder button, locked while there is no dirty money to launder")]
    [SerializeField] private MenuButtonFx launderButton;
    [Header("Audio")]
    [Tooltip("Plays one-shot sounds for the shop")]
    [SerializeField] private AudioSource sfxSource;
    [Tooltip("Played when an item is bought")]
    [SerializeField] private AudioClip purchaseSound;
    [Tooltip("Played when dirty money is laundered")]
    [SerializeField] private AudioClip launderSound;
    [Tooltip("Tab locked as coming soon. Its page is kept but cannot be opened. Empty: every tab is open")]
    [SerializeField] private MenuButtonFx comingSoonTab;

    private void Awake()
    {
        if (tabs == null || pages == null || tabs.Length != pages.Length)
        {
            Debug.LogWarning($"{name} needs one page per tab", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (wallet == null) return;
        wallet.Changed += RefreshLaunder;
        RefreshLaunder();
    }

    private void OnDisable()
    {
        if (wallet != null) wallet.Changed -= RefreshLaunder;
    }

    private void RefreshLaunder()
    {
        if (launderButton != null) launderButton.SetLocked(wallet.Dirty <= 0);
    }

    private void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].Button.onClick.AddListener(() => ShowTab(index));
        }
        if (comingSoonTab != null) comingSoonTab.SetLocked(true);
        ShowTab(0);
    }

    public void ShowTab(int index)
    {
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == index);
            tabs[i].SetSelected(i == index);
        }
        detailTitle.text = "";
        detailBody.text = "<color=#9A9A9A>Hover an item for details.</color>";
    }

    // A locked item names what it needs, since its price label only says LOCKED
    public void ShowDetail(ShopItem item)
    {
        detailTitle.text = item.IsLocked
            ? $"{item.Title.ToUpperInvariant()}  <color=#7A7A7A>NEEDS {item.Requires.Title.ToUpperInvariant()}</color>"
            : item.Title.ToUpperInvariant();
        detailBody.text = item.Description;
    }

    public void Launder()
    {
        if (wallet != null && wallet.TryLaunder())
            Play(launderSound);

    }

    public void PlayPurchase() => Play(purchaseSound);

    private void Play(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip);
    }
}
