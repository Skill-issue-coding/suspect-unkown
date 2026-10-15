using System;
using System.Collections.Generic;
using UnityEngine;

// The job's witnesses and, once the job ends, their merged description: what the police know of the
// player's appearance from this job. Every enabled NpcWitness registers here; when JobOutcome ends the job,
// their reports merge in log-odds and Described hands the result on (the witness sketch). Also keeps the
// police record across jobs: loaded when the first witness registers, and on a completed job faded by
// RecordKeptPerJob, merged with this job's description and saved. A failed job only produces the
// description (the mugshot). Listens to JobOutcome only while a witness exists, because JobOutcome clears
// its events on play start, before any witness has registered
public static class JobWitnesses
{
    private static readonly List<NpcWitness> witnesses = new();

    // How much of the police record survives each job: 0.5 halves the evidence, so a sure description
    // drops to "probably" after one job and out of the caption after about three (burns are temporary)
    private const float RecordKeptPerJob = 0.5f;

    // What the police knew when this job started, from earlier jobs; empty on a first run
    public static WitnessReport Record { get; private set; }

    // Raised once at job end with the merged description, after Description is set
    public static event Action<WitnessReport> Described;

    // The job's police description; null until the job ends
    public static WitnessReport Description { get; private set; }

    public static void Register(NpcWitness witness)
    {
        if (witnesses.Contains(witness)) return;
        if (witnesses.Count == 0)
        {
            Record = PoliceStore.Load();
            JobOutcome.Failed += OnFailed;
            JobOutcome.Completed += OnCompleted;
            Debug.Log(Record.SawAnything ? $"Police record at job start:\n{Record}" : "Police record at job start: nothing on file");
        }
        witnesses.Add(witness);
    }

    public static void Unregister(NpcWitness witness)
    {
        if (!witnesses.Remove(witness) || witnesses.Count > 0) return;
        JobOutcome.Failed -= OnFailed;
        JobOutcome.Completed -= OnCompleted;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        witnesses.Clear();
        Description = null;
        Described = null;
        Record = null;
    }

    private static void OnFailed(FailReason reason) => OnEnded(false);
    private static void OnCompleted() => OnEnded(true);

    private static void OnEnded(bool completed)
    {
        WitnessReport merged = new WitnessReport();
        foreach (NpcWitness witness in witnesses) merged.Merge(witness.Report());
        Description = merged;
        Debug.Log(merged.SawAnything ? $"Police description:\n{merged}" : "Police description: no witnesses came forward");
        Described?.Invoke(merged);

        // Only a getaway reaches the record, as with progress.json: a failed job leaves nothing on file, or a
        // level could turn unwinnable once the player is described well enough to be recognised at once
        if (!completed) return;

        // The old record fades by a job, then this job's evidence adds to it: a fresh good look outweighs
        // an old one, and an outfit described differently now takes over
        WitnessReport record = Record ?? PoliceStore.Load();
        record.Fade(RecordKeptPerJob);
        record.Merge(merged);
        PoliceStore.Save(record);
        Record = record;
    }
}
