using System;
using System.IO;
using UnityEngine;

// Loads and saves the police record, what the police know of the player's appearance across jobs, as
// police.json beside progress.json. Its own file, so a broken record never costs the player's progress. A
// missing, unreadable or outdated file loads as an empty record. The only code that touches it on disk
public static class PoliceStore
{
    private const string FileName = "police.json";

    // A property, not a static field: Unity may not be ready when static fields initialise
    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static WitnessReport Load()
    {
        if (!File.Exists(FilePath)) return new WitnessReport();

        try
        {
            WitnessReport record = JsonUtility.FromJson<WitnessReport>(File.ReadAllText(FilePath));
            if (record != null && record.IsWellFormed()) return record;

            Debug.LogWarning($"Police record: {FilePath} does not match today's outfit slots, starting empty");
            return new WitnessReport();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Police record: could not read {FilePath}, starting empty. {e.Message}");
            return new WitnessReport();
        }
    }

    // Warns instead of throwing: a full disk or a locked file must not break the end of a job
    public static void Save(WitnessReport record)
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(record, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Police record: could not write {FilePath}. {e.Message}");
        }
    }

    // For the editor's Police Record menu: the next Load starts empty
    public static void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
