using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Editor menu for the save file, so progress can be seen and reset while testing: 
// print what is saved, open the folder it lives in, delete it to play as a first-time 
// player, unlock everything to check unlocked content, or fake a finished job so the menu's
// debrief can be checked and tuned without replaying one.
public static class ProgressMenu
{
    [MenuItem(MenuPaths.ProgressLog, priority = MenuPaths.ProgressPriority)]
    private static void Log() =>
        Debug.Log($"Progress at {ProgressStore.FilePath}:\n{JsonUtility.ToJson(ProgressStore.Load(), true)}");

    // The folder, not the file, so it works before any file exists
    [MenuItem(MenuPaths.ProgressReveal, priority = MenuPaths.ProgressPriority + 1)]
    private static void Reveal() => EditorUtility.RevealInFinder(Application.persistentDataPath);

    [MenuItem(MenuPaths.ProgressDelete, priority = MenuPaths.ProgressPriority + 2)]
    private static void Delete()
    {
        ProgressStore.Delete();
        Debug.Log($"Progress deleted at {ProgressStore.FilePath}");
    }

    // Every flag on, so the menu's unlocked state can be checked without replaying jobs
    [MenuItem(MenuPaths.ProgressUnlockAll, priority = MenuPaths.ProgressPriority + 3)]
    private static void UnlockAll()
    {
        ProgressSave save = ProgressStore.Load();
        save.Apply((ProgressUnlock)~0);
        ProgressStore.Save(save);
        Debug.Log("Progress: everything unlocked");
    }

    // A sample apartment run as lastJob, with the debrief pending: the next menu start opens on it. Six loot
    // lines, so the take's list scrolls, and a vague witness. Adds no money or XP; the bar starts from what the
    // save already holds less this job's XP, and never below 0
    [MenuItem(MenuPaths.ProgressFakeDebrief, priority = MenuPaths.ProgressPriority + 4)]
    private static void FakeDebrief()
    {
        ProgressSave save = ProgressStore.Load();
        save.lastJob = new JobResult
        {
            job = "Apartment",
            seconds = 161f,
            loot = new List<JobLoot>
            {
                new JobLoot { name = "Laptop", count = 1, value = 400, objective = true },
                new JobLoot { name = "Cellphone", count = 1, value = 150, objective = true },
                new JobLoot { name = "Cash", count = 6, value = 180 },
                new JobLoot { name = "Watch", count = 1, value = 220 },
                new JobLoot { name = "Jewelry box", count = 1, value = 310 },
                new JobLoot { name = "Perfume", count = 2, value = 90 },
            },
            peakAlert = JobAlertLevel.Searching,
            witness = WitnessClarity.Vague,
            witnessCaption = "Witness description: possibly brown hair, possibly red jacket.",
        };
        save.debriefPending = true;
        ProgressStore.Save(save);
        Debug.Log("Progress: fake debrief written; play the main menu to see it");
    }
}
