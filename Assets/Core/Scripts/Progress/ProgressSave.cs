using System;
using System.Collections.Generic;

// Everything that carries over between jobs, as written to progress.json. Plain data only:
// ProgressStore reads and writes it, and nothing here touches the disk
[Serializable]
public class ProgressSave
{
    public bool shopUnlocked;
    public bool targetsUnlocked;
    public int money;
    public List<string> completedJobs = new();

    public void Apply(ProgressUnlock unlocks)
    {
        if ((unlocks & ProgressUnlock.Shop) != 0) shopUnlocked = true;
        if ((unlocks & ProgressUnlock.Targets) != 0) targetsUnlocked = true;
    }
}
