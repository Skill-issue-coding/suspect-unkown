using System;
using System.IO;
using UnityEngine;

// Loads and saves SecuritySave as security.json in the player's persistent data folder, a file of its own
// beside progress.json and police.json so a broken file never costs the player's progress. A missing or
// unreadable file loads as an empty save: security then knows nothing, as on a first run
public static class SecurityStore
{
    private const string FileName = "security.json";

    // A property, not a static field: Unity may not be ready when static fields initialise
    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static SecuritySave Load()
    {
        if (!File.Exists(FilePath)) return new SecuritySave();

        try
        {
            // An empty file parses to null rather than throwing
            return JsonUtility.FromJson<SecuritySave>(File.ReadAllText(FilePath)) ?? new SecuritySave();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Security: could not read {FilePath}, starting fresh. {e.Message}");
            return new SecuritySave();
        }
    }

    // Warns instead of throwing: a full disk or a locked file must not break a successful job
    public static void Save(SecuritySave save)
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(save, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Security: could not write {FilePath}. {e.Message}");
        }
    }

    // For the editor's Security Record menu: the next Load starts empty
    public static void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
