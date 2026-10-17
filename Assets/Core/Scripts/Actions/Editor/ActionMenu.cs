using UnityEditor;
using UnityEngine;

// Editor menu for the action record, so it can be seen and reset while testing: print what security has
// learned per location, open the save folder, or delete it to play every location as a first run
public static class ActionMenu
{
    [MenuItem(MenuPaths.ActionRecordLog, priority = MenuPaths.ActionRecordPriority)]
    private static void Log()
    {
        ActionSave save = ActionStore.Load();
        Debug.Log(save.locations.Count > 0
            ? $"Action record at {ActionStore.FilePath}:\n{JsonUtility.ToJson(save, true)}"
            : $"Action record at {ActionStore.FilePath}: nothing on file");
    }

    [MenuItem(MenuPaths.ActionRecordReveal, priority = MenuPaths.ActionRecordPriority + 1)]
    private static void Reveal() => EditorUtility.RevealInFinder(ActionStore.FilePath);

    [MenuItem(MenuPaths.ActionRecordDelete, priority = MenuPaths.ActionRecordPriority + 2)]
    private static void Delete()
    {
        ActionStore.Delete();
        Debug.Log($"Action record deleted at {ActionStore.FilePath}");
    }
}
