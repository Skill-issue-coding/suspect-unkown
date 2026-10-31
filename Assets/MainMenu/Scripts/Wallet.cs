using System;
using System.Globalization;
using UnityEngine;

// Concept wallet (concept 7.1): the take is dirty and cannot be spent until a fence
// launders it at a loss. Clean cash buys gear and abilities, skill points buy skills.
// progress.json is the only truth. The wallet keeps no copy of the save: every write loads the file,
// changes only its own fields and saves at once, so it never writes back what another script (the debrief,
// a finished job) changed in the meantime. Loads lazily, since a ShopItem's OnEnable may run before this
// Awake: Unity runs Awake and OnEnable per object, in no fixed order between objects
public class Wallet : MonoBehaviour
{
    public enum Currency { Clean, SkillPoints }

    private const float BaseFenceRate = 0.65f;
    private const float NegotiatorFenceRate = 0.85f;

    // Runtime state read from the save, never serialized: inspector values here once leaked into progress.json
    private int dirty;
    private int clean;
    private int skillPoints;
    private float fenceRate;
    private bool loaded;

    public event Action Changed;

    public int Dirty { get { EnsureLoaded(); return dirty; } }
    public int Clean { get { EnsureLoaded(); return clean; } }
    public int SkillPoints { get { EnsureLoaded(); return skillPoints; } }
    public float FenceRate { get { EnsureLoaded(); return fenceRate; } }

    private void Awake() => EnsureLoaded();

    private void EnsureLoaded()
    {
        if (loaded) return;
        ReloadFromDisk();
    }

    private void ReloadFromDisk()
    {
        ProgressSave save = ProgressStore.Load();
        clean = save.money;
        dirty = save.dirtyMoney;
        skillPoints = save.skillPoints;
        fenceRate = Mathf.Max(fenceRate, FenceRateFor(save));
        loaded = true;
        Changed?.Invoke();
    }

    public bool HasUnlock(ProgressUnlock unlock) =>
        unlock != ProgressUnlock.None && ProgressStore.Load().Has(unlock);

    // Writes the flag and nothing else, so it can never overwrite money or anything another script saved
    public void Unlock(ProgressUnlock unlock)
    {
        if (unlock == ProgressUnlock.None) return;
        EnsureLoaded();

        ProgressSave save = ProgressStore.Load();
        save.Apply(unlock);
        ProgressStore.Save(save);

        fenceRate = Mathf.Max(fenceRate, FenceRateFor(save));
        Changed?.Invoke();
    }

    // Invariant culture on purpose: a Swedish locale groups thousands with a no-break space,
    // which the static ASCII Electrolize atlas does not contain
    public static string Money(int dollars) => "$" + dollars.ToString("N0", CultureInfo.InvariantCulture);

    public bool CanAfford(Currency currency, int amount)
    {
        EnsureLoaded();
        return (currency == Currency.Clean ? clean : skillPoints) >= amount;
    }

    public bool TrySpend(Currency currency, int amount)
    {
        if (!CanAfford(currency, amount)) return false;
        if (currency == Currency.Clean) clean -= amount;
        else skillPoints -= amount;
        SaveMoney();
        Changed?.Invoke();
        return true;
    }

    public bool TryLaunder()
    {
        EnsureLoaded();
        if (dirty <= 0) return false;
        clean += Mathf.RoundToInt(dirty * fenceRate);
        dirty = 0;
        SaveMoney();
        Changed?.Invoke();
        return true;
    }

    public void ImproveFence(float rate)
    {
        EnsureLoaded();
        fenceRate = Mathf.Max(fenceRate, rate);
        Changed?.Invoke();
    }

    // Load, change only the three money fields, save
    private void SaveMoney()
    {
        ProgressSave save = ProgressStore.Load();
        save.money = clean;
        save.dirtyMoney = dirty;
        save.skillPoints = skillPoints;
        ProgressStore.Save(save);
    }

    private static float FenceRateFor(ProgressSave save) =>
        save.Has(ProgressUnlock.Skill_Negotiator) ? NegotiatorFenceRate : BaseFenceRate;
}
