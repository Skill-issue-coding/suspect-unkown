using System;
using System.Collections.Generic;

// What happened on a completed job, saved for the menu's debrief: the take, the time, the highest alert
// and how well the witnesses saw the player. Facts only, never XP: the XP is worked out from these when
// shown, so changing the XP rules never leaves an old save with stale numbers
[Serializable]
public class JobResult
{
    public string job;
    public float seconds;
    public List<JobLoot> loot = new();
    public JobAlertLevel peakAlert;
    public WitnessClarity witness;
    public string witnessCaption;
}