using System;
using System.IO;
using UnityEngine;

// Loads and saves ProgressSave as progress.json in the player's persistent data folder. The only code
// that touches the save on disk. A missing or unreadable file loads as a fresh save, so a first run
// and a broken file both start clean instead of throwing
public static class ProgressStore
{
    private const string FileName = "progress.json";

    // A property, not a static field: Unity may not be ready when static fields initialise
    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static ProgressSave Load()
    {
        if (!File.Exists(FilePath)) return new ProgressSave();

        try
        {
            // An empty file parses to null rather than throwing
            return JsonUtility.FromJson<ProgressSave>(File.ReadAllText(FilePath)) ?? new ProgressSave();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Progress: could not read {FilePath}, starting fresh. {e.Message}");
            return new ProgressSave();
        }
    }

    // Warns instead of throwing: a full disk or a locked file must not break a successful job
    public static void Save(ProgressSave save)
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(save, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Progress: could not write {FilePath}. {e.Message}");
        }
    }

    // For the editor's Progress menu: the next Load starts fresh
    public static void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
