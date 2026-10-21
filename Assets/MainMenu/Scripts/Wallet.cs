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
    [SerializeField] private int dirty = 0;
    [Tooltip("Spendable money, dollars")]
    [SerializeField] private int clean = 0;
    [Tooltip("Unspent skill points")]
    [SerializeField] private int skillPoints = 0;
    [Tooltip("Fraction of dirty money the fence pays out")]
    [Range(0f, 1f)]
    [SerializeField] private float fenceRate = 0.65f;

    public ProgressSave save;

    public event Action Changed;

    public int Dirty => dirty;
    public int Clean => clean;
    public int SkillPoints => skillPoints;
    public float FenceRate => fenceRate;

    private void Awake()
    {
        ReloadFromDisk();
    }

    public void ReloadFromDisk()
    {
        save = ProgressStore.Load();
        clean = save.money;
        dirty = save.dirtyMoney;
        skillPoints = save.skillPoints;
        fenceRate = save.Has(ProgressUnlock.Skill_Negotiator) ? 0.85f : 0.65f;
        Changed?.Invoke();
    }

    public void SavetoDisk()
    {
        if (save == null) save = ProgressStore.Load();
        save.money = clean;
        save.dirtyMoney = dirty;
        save.skillPoints = skillPoints;
        ProgressStore.Save(save);
    }

    public bool HasUnlock(ProgressUnlock unlock)
    {
        if (save == null) save = ProgressStore.Load();
        return unlock != ProgressUnlock.None && save.Has(unlock);
    }

    public void Unlock(ProgressUnlock unlock)
    {
        if (unlock == ProgressUnlock.None) return;
        if (save == null) save = ProgressStore.Load();
        save.Apply(unlock);
        fenceRate = save.Has(ProgressUnlock.Skill_Negotiator) ? 0.85f : 0.65f;
        SavetoDisk();
        Changed?.Invoke();
    }

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
        SavetoDisk();
        Changed?.Invoke();
        return true;
    }

    public void Launder()
    {
        if (dirty <= 0) return;
        clean += Mathf.RoundToInt(dirty * fenceRate);
        dirty = 0;
        SavetoDisk();
        Changed?.Invoke();
    }

    public void ImproveFence(float rate)
    {
        fenceRate = Mathf.Max(fenceRate, rate);
        Changed?.Invoke();
    }
}
