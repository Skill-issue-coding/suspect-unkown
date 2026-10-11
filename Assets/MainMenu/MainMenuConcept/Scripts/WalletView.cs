using UnityEngine;
using TMPro;

// Shows the wallet in one place (the HUD corner, the shop header). Updates on the
// wallet's Changed event only. A label whose authored text contains {0} keeps that text
// as its format, so "Dirty money {0}" stays readable in the scene and at runtime.
public class WalletView : MonoBehaviour
{
    [Tooltip("Wallet to show")]
    [SerializeField] private Wallet wallet;
    [Tooltip("Labels, any may be empty")]
    [SerializeField] private TMP_Text dirty, clean, skillPoints, fence;

    private string dirtyFormat, cleanFormat;

    private void Awake()
    {
        dirtyFormat = FormatOf(dirty);
        cleanFormat = FormatOf(clean);
    }

    private void OnEnable()
    {
        if (wallet == null) return;
        wallet.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (wallet != null) wallet.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (dirty != null) dirty.text = string.Format(dirtyFormat, Wallet.Money(wallet.Dirty));
        if (clean != null) clean.text = string.Format(cleanFormat, Wallet.Money(wallet.Clean));
        if (skillPoints != null) skillPoints.text = wallet.SkillPoints.ToString();
        if (fence != null) fence.text = $"Fence pays {Mathf.RoundToInt(wallet.FenceRate * 100f)}%";
    }

    private static string FormatOf(TMP_Text label) =>
        label != null && label.text.Contains("{0}") ? label.text : "{0}";
}
