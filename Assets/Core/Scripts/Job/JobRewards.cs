using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The job's takings: every item taken this run, required or extra, adds its value and unlocks here.
// They reach the save only when the job succeeds; a failed job writes nothing. One per level, on the
// Job object. Hears LootPickup.Taken and JobOutcome.Completed, so nothing needs a reference to it
public class JobRewards : MonoBehaviour
{
    private int cleanMoney;
    private int dirtyMoney;
    private ProgressUnlock unlocks;

    private readonly JobResult result = new();
    private readonly List<XpLine> xpLines = new();
    private JobObjectives objectives;
    public bool HasBagLoot => dirtyMoney > 0;
    public bool HasCashOnly => cleanMoney > 0 && dirtyMoney == 0;

    private void Awake()
    {
        objectives = GetComponent<JobObjectives>();
        if (objectives == null)
            Debug.LogWarning($"{name} has no JobObjectives beside it; every item counts as bonus loot", this);
    }

    private void OnEnable()
    {
        LootPickup.Taken += OnTaken;
        JobOutcome.Completed += OnCompleted;
    }

    private void OnDisable()
    {
        LootPickup.Taken -= OnTaken;
        JobOutcome.Completed -= OnCompleted;
    }

    private void OnTaken(LootPickup pickup)
    {
        if (pickup.Item.CarriedIn == LootCarry.Pocket)
        {
            cleanMoney += pickup.Item.Value;
        }
        else
        {
            dirtyMoney += pickup.Item.Value;
        }

        unlocks |= pickup.Item.ProgressUnlock;
        AddLoot(pickup.Item);
    }

    // One line per item kind: a second pickup of the same item (cash) adds to its line. Objectives go to the
    // front, in the order taken, so the debrief lists them first
    private void AddLoot(LootItem item)
    {
        for (int i = 0; i < result.loot.Count; i++)
        {
            if (result.loot[i].name != item.DisplayName) continue;
            JobLoot line = result.loot[i];
            line.count++;
            line.value += item.Value;
            result.loot[i] = line;
            return;
        }

        bool objective = IsObjective(item);
        JobLoot added = new JobLoot { name = item.DisplayName, count = 1, value = item.Value, objective = objective };
        if (objective) result.loot.Insert(CountObjectives(), added);
        else result.loot.Add(added);
    }

    // True when this job's objective list contains the item
    private bool IsObjective(LootItem item)
    {
        if (objectives == null) return false;

        for (int i = 0; i < objectives.Required; i++)
            if (objectives.RequiredItem(i) == item) return true;
        return false;
    }

    // How many lines are objectives, which are all at the front: the index where the next objective goes
    private int CountObjectives()
    {
        int count = 0;
        foreach (JobLoot line in result.loot)
            if (line.objective) count++;
        return count;
    }

    private void OnCompleted()
    {
        // Now: the clock stops here, and Peak is the job's highest so far
        result.job = gameObject.scene.name;
        result.seconds = Time.timeSinceLevelLoad;
        result.peakAlert = JobAlert.Peak;
        StartCoroutine(WriteResult());
    }

    // Witnesses report on the same Completed event in no fixed order; one frame later every listener has run
    private IEnumerator WriteResult()
    {
        yield return null;
        result.witness = JobNews.Clarity;
        result.witnessCaption = JobNews.WitnessCaption;
        int xp = XpRules.Score(result, xpLines);

        ProgressSave save = ProgressStore.Load();
        int levelBefore = Reputation.LevelFor(save.xp, out _);
        save.money += cleanMoney;
        save.dirtyMoney += dirtyMoney;
        save.Apply(unlocks);
        save.xp += xp;
        save.skillPoints += Reputation.LevelFor(save.xp, out _) - levelBefore;
        save.lastJob = result;
        save.debriefPending = true;
        if (!save.completedJobs.Contains(result.job)) save.completedJobs.Add(result.job);

        ProgressStore.Save(save);
        Debug.Log($"Progress saved: +${cleanMoney}, +${dirtyMoney} +{xp} XP, unlocks {unlocks}, witness {result.witness}, job {result.job}");
    }
}
