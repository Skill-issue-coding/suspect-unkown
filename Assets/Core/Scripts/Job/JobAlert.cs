using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// How alarmed the job is right now: the highest level any NPC reports, the highest level it has reached this
// job (Peak), and the fullest decision window any NPC has open (how close someone is to calling the police).
// Each NPC reports for itself (NpcAlertReporter, NpcWindowReporter, NPC side) and withdraws when it goes
// away; the HUD listens to Changed and reads the window while one is open. Core never knows about NPCs, and
// one clerk or a store full of guards works the same way. Cleared on play-mode entry; on a scene load the NPCs
// withdraw as they are destroyed, and Peak is cleared so a restart starts a fresh job
public static class JobAlert
{
    private static readonly Dictionary<object, JobAlertLevel> reports = new();
    private static readonly Dictionary<object, float> windows = new();
    private static readonly HashSet<object> callers = new();

    public static JobAlertLevel Level { get; private set; }

    // The highest level reached this job, for the debrief: Level falls back as NPCs calm down, this never does
    public static JobAlertLevel Peak { get; private set; }

    public static event Action<JobAlertLevel, JobAlertLevel> Changed;

    // Once per reaction, for the HUD's toast. No source: the toast does not care who it was
    public static event Action<JobReaction> Reacted;

    public static void Report(object source, JobAlertLevel level)
    {
        reports[source] = level;
        Recount();
    }

    public static void Withdraw(object source)
    {
        if (reports.Remove(source)) Recount();
    }

    // A handful of NPCs and only on a state change, so a plain scan beats keeping a sorted structure
    private static void Recount()
    {
        JobAlertLevel highest = JobAlertLevel.Undetected;
        foreach (JobAlertLevel level in reports.Values)
            if (level > highest) highest = level;

        if (highest > Peak) Peak = highest;
        if (highest == Level) return;
        JobAlertLevel from = Level;
        Level = highest;
        Changed?.Invoke(from, highest);
    }

    public static bool AnyWindowOpen => windows.Count > 0;

    public static void ReportReaction(JobReaction reaction) => Reacted?.Invoke(reaction);

    // progress: 0 to 1, how full the window is (1 once the call is under way). calling: the call is under way
    public static void ReportWindow(object source, float progress, bool calling)
    {
        windows[source] = progress;
        if (calling) callers.Add(source);
        else callers.Remove(source);
    }

    public static void CloseWindow(object source)
    {
        windows.Remove(source);
        callers.Remove(source);
    }

    // The fullest open window, read by the HUD each frame while one is open. A plain scan over a handful
    public static void Window(out float progress, out bool calling)
    {
        progress = 0f;
        foreach (float p in windows.Values)
            if (p > progress) progress = p;
        calling = callers.Count > 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        windows.Clear();
        callers.Clear();
        reports.Clear();
        Reacted = null;
        Level = JobAlertLevel.Undetected;
        Peak = JobAlertLevel.Undetected;
        Changed = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // A restart reloads the scene but keeps statics: Level follows the NPCs as they withdraw, Peak must be reset
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Peak = JobAlertLevel.Undetected;
    }
}
