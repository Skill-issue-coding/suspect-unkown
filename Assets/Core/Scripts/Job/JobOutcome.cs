using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class JobOutcome
{
    public static event Action<FailReason> Failed;
    public static event Action Completed;
    public static event Action Exited;

    public static bool HasEnded { get; private set; }

    public static void Fail(FailReason reason)
    {
        if (HasEnded) return;
        HasEnded = true;
        Debug.Log($"Job failed: {reason}");
        Failed?.Invoke(reason);
    }

    public static void Complete()
    {
        if (HasEnded) return;
        HasEnded = true;
        Debug.Log("Job completed");
        Completed?.Invoke();
    }

    public static void Exit()
    {
        if (HasEnded) return;
        HasEnded = true;
        Debug.Log("Job Exited");
        Exited?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        HasEnded = false;
        Failed = null;
        Completed = null;
        Exited = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) HasEnded = false;
    }
}