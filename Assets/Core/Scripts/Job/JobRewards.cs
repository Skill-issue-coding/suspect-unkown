using UnityEngine;

// The job's takings: every item taken this run, required or extra, adds its value and unlocks here.
// They reach the save only when the job succeeds; a failed job writes nothing. One per level, on the
// Job object. Hears LootPickup.Taken and JobOutcome.Completed, so nothing needs a reference to it
public class JobRewards : MonoBehaviour
{
    private int money;
    private ProgressUnlock unlocks;

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
        money += pickup.Item.Value;
        unlocks |= pickup.Item.ProgressUnlock;
    }

    private void OnCompleted()
    {
        ProgressSave save = ProgressStore.Load();
        save.money += money;
        save.Apply(unlocks);

        string job = gameObject.scene.name;
        if (!save.completedJobs.Contains(job)) save.completedJobs.Add(job);

        ProgressStore.Save(save);
        Debug.Log($"Progress saved: +${money}, unlocks {unlocks}, job {job}");
    }
}
