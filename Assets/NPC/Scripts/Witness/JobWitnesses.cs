using System;
using System.Collections.Generic;
using UnityEngine;

// The job's witnesses and, once the job ends, their merged description: what the police know of the
// player's appearance from this job. Every enabled NpcWitness registers here; when JobOutcome ends the job,
// their reports merge in log-odds, the player's Forgettable skill scales the result, and Described hands it
// on (the witness sketch). Also keeps the
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

    // SuspectSketch's vagueAt: a part the newspaper could not print cannot be recognised
    private const float RecogniseFrom = 0.2f;

    // The least recognition weight any observer who recognises outfits gives a burned one, whatever its
    // archetype says
    public const float BurnedRecognition = 0.9f;

    private static float recordMatch;
    private static bool recordBurned;
    private static bool matched;

    // What the police knew when this job started, from earlier jobs; empty on a first run
    public static WitnessReport Record { get; private set; }

    // Raised once at job end with the merged description, after Description is set
    public static event Action<WitnessReport> Described;

    // The job's police description; null until the job ends
    public static WitnessReport Description { get; private set; }

    // How well the worn outfit fits what the police knew at job start, 0 to 1 (thrift shop §6). Worked out on
    // the first call and kept: the record is fixed for the job, and so is the outfit (decision 23). Not when
    // the record loads, because PlayerAppearance reads the outfit in its Start, after witnesses register
    public static float RecordMatch(CharacterAppearance worn)
    {
        MatchOnce(worn);
        return recordMatch;
    }

    // True when the record pins down at least BurnedPartsToKnow worn parts: the outfit is known on sight, and
    // every observer who recognises weighs it at least BurnedRecognition. Kept like RecordMatch
    public static bool IsBurned(CharacterAppearance worn)
    {
        MatchOnce(worn);
        return recordBurned;
    }

    private static void MatchOnce(CharacterAppearance worn)
    {
        if (matched) return;
        if (Record == null)
        {
            // No witness has registered yet: nothing on file to match, and nothing kept
            recordMatch = 0f;
            recordBurned = false;
            return;
        }

        recordMatch = Record.Match(worn, RecogniseFrom);
        recordBurned = Record.BurnedCount(worn) >= WitnessReport.BurnedPartsToKnow;
        matched = true;
    }

    public static void Register(NpcWitness witness)
    {
        if (witnesses.Contains(witness)) return;
        if (witnesses.Count == 0)
        {
            Record = PoliceStore.Load();
            matched = false;
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
        matched = false;
    }

    private static void OnFailed(FailReason reason) => OnEnded(false);
    private static void OnCompleted() => OnEnded(true);

    private static void OnEnded(bool completed)
    {
        WitnessReport merged = new WitnessReport();
        foreach (NpcWitness witness in witnesses) merged.Merge(witness.Report());

        // Forgettable: the police keep only part of what the witnesses saw. Scaling log-odds is the same
        // operation as fading them, so Fade does it. Once per job, so the lookup is not on a hot path
        PlayerSkills skills = UnityEngine.Object.FindAnyObjectByType<PlayerSkills>();
        if (skills != null) merged.Fade(skills.WitnessEvidenceFactor);

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
