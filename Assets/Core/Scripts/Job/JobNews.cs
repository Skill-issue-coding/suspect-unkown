using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// What the newspaper reports about a job beyond how it ended: for now, the witness caption. Filled by the NPC
// side at job end, so Core never references witness types; the picture itself arrives through a
// RenderTexture asset. The newspaper listens for Changed, because the outcome and the description arrive on
// the same JobOutcome event in no fixed order. Empty means no witness came forward
public static class JobNews
{
    public static event Action Changed;

    public static bool HasWitness { get; private set; }
    public static string WitnessCaption { get; private set; }

    public static void ReportWitness(string caption)
    {
        HasWitness = true;
        WitnessCaption = caption;
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Clear();
        Changed = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // A restart reloads the scene but keeps statics: the next job starts with no news
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Clear();
    }

    private static void Clear()
    {
        HasWitness = false;
        WitnessCaption = null;
    }
}
