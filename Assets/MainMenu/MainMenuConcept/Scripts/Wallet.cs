using System;
using System.Globalization;
using UnityEngine;

// Concept wallet (concept 7.1): the take is dirty and cannot be spent until a fence
// launders it at a loss. Clean cash buys gear and abilities, skill points buy skills.
// Mock-up state only; the real values live in the save.
public class Wallet : MonoBehaviour
{
    public enum Currency { Clean, SkillPoints }

    [Tooltip("Unlaundered take, dollars")]
    [SerializeField] private int dirty = 8400;
    [Tooltip("Spendable money, dollars")]
    [SerializeField] private int clean = 2750;
    [Tooltip("Unspent skill points")]
    [SerializeField] private int skillPoints = 2;
    [Tooltip("Fraction of dirty money the fence pays out")]
    [Range(0f, 1f)]
    [SerializeField] private float fenceRate = 0.65f;

    public event Action Changed;

    public int Dirty => dirty;
    public int Clean => clean;
    public int SkillPoints => skillPoints;
    public float FenceRate => fenceRate;

    // Invariant culture on purpose: a Swedish locale groups thousands with a no-break space,
    // which the static ASCII Electrolize atlas does not contain
    public static string Money(int dollars) => "$" + dollars.ToString("N0", CultureInfo.InvariantCulture);

    public bool CanAfford(Currency currency, int amount) =>
        (currency == Currency.Clean ? clean : skillPoints) >= amount;

    public bool TrySpend(Currency currency, int amount)
    {
        if (!CanAfford(currency, amount)) return false;
        if (currency == Currency.Clean) clean -= amount;
        else skillPoints -= amount;
        Changed?.Invoke();
        return true;
    }

    public void Launder()
    {
        if (dirty <= 0) return;
        clean += Mathf.RoundToInt(dirty * fenceRate);
        dirty = 0;
        Changed?.Invoke();
    }

    public void ImproveFence(float rate)
    {
        fenceRate = Mathf.Max(fenceRate, rate);
        Changed?.Invoke();
    }
}
