using System;
using System.IO;
using UnityEngine;

// Loads and saves ActionSave as actions.json in the player's persistent data folder, a file of its own
// beside progress.json and police.json so a broken file never costs the player's progress. A missing or
// unreadable file loads as an empty save: security then knows nothing, as on a first run
public static class ActionStore
{
    private const string FileName = "actions.json";

    // A property, not a static field: Unity may not be ready when static fields initialise
    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static ActionSave Load()
    {
        if (!File.Exists(FilePath)) return new ActionSave();

        try
        {
            // An empty file parses to null rather than throwing
            return JsonUtility.FromJson<ActionSave>(File.ReadAllText(FilePath)) ?? new ActionSave();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Action record: could not read {FilePath}, starting fresh. {e.Message}");
            return new ActionSave();
        }
    }

    // Warns instead of throwing: a full disk or a locked file must not break a successful job
    public static void Save(ActionSave save)
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(save, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Action record: could not write {FilePath}. {e.Message}");
        }
    }

    // For the editor's Action Record menu: the next Load starts empty
    public static void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
