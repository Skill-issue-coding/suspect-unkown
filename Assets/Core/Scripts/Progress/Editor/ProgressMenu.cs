using UnityEditor;
using UnityEngine;

// Editor menu for the save file, so progress can be seen and reset while testing: print what is saved,
// open the folder it lives in, or delete it to play as a first-time player
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
}
