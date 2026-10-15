using UnityEditor;
using UnityEngine;

// Editor menu for the police record, so it can be seen and reset while testing: print what the police know,
// or delete it to play as a player nobody has described yet
public static class PoliceRecordMenu
{
    [MenuItem(MenuPaths.PoliceLog, priority = MenuPaths.PolicePriority)]
    private static void Log()
    {
        WitnessReport record = PoliceStore.Load();
        Debug.Log(record.SawAnything ? $"Police record at {PoliceStore.FilePath}:\n{record}" : $"Police record at {PoliceStore.FilePath}: nothing on file");
    }

    [MenuItem(MenuPaths.PoliceDelete, priority = MenuPaths.PolicePriority + 1)]
    private static void Delete()
    {
        PoliceStore.Delete();
        Debug.Log($"Police record deleted at {PoliceStore.FilePath}");
    }
}
