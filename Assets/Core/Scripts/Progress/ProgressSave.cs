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
    public int xp;
    public int skillPoints;
    public JobResult lastJob = new();
    public bool debriefPending;
    public List<string> completedJobs = new();

    // None counts as unlocked, so content that needs nothing (the Apartment) is always open
    public bool Has(ProgressUnlock unlock) => (unlocks & unlock) == unlock;

    public void Apply(ProgressUnlock unlock) => unlocks |= unlock;
}
