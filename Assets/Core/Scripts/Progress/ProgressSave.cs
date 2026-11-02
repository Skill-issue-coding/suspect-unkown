using System;
using System.Collections.Generic;

// Everything that carries over between jobs, as written to progress.json. Plain data only:
// ProgressStore reads and writes it, and nothing here touches the disk Unlocks are flags; 
// Has and Apply are the only way in
[Serializable]
public class ProgressSave
{
    public ProgressUnlock unlocks;
    public int money;
    public int dirtyMoney;
    public int xp;
    public int skillPoints;
    public JobResult lastJob = new();
    public bool debriefPending;
    // One entry per completed run, by scene name, so the count per level is how often it appears
    public List<string> completedJobs = new();
    // Outfit parts bought in the wardrobe, by part name (Chest_002_Red). Free parts are never listed
    public List<string> ownedOutfitParts = new();

    public bool OwnsOutfitPart(string partName) => ownedOutfitParts.Contains(partName);

    public int TimesCompleted(string job)
    {
        int count = 0;
        foreach (string done in completedJobs)
            if (done == job) count++;
        return count;
    }

    // None counts as unlocked, so content that needs nothing (the Apartment) is always open
    public bool Has(ProgressUnlock unlock) => (unlocks & unlock) == unlock;

    public void Apply(ProgressUnlock unlock) => unlocks |= unlock;
}
