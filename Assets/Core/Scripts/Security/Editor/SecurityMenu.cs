using UnityEditor;
using UnityEngine;

// Editor menu for the security record, so it can be seen and reset while testing: print what security has
// learned per location, open the save folder, or delete it to play every location as a first run
public static class SecurityMenu
{
    [MenuItem(MenuPaths.SecurityLog, priority = MenuPaths.SecurityPriority)]
    private static void Log()
    {
        SecuritySave save = SecurityStore.Load();
        Debug.Log(save.locations.Count > 0
            ? $"Security record at {SecurityStore.FilePath}:\n{JsonUtility.ToJson(save, true)}"
            : $"Security record at {SecurityStore.FilePath}: nothing on file");
    }

    [MenuItem(MenuPaths.SecurityReveal, priority = MenuPaths.SecurityPriority + 1)]
    private static void Reveal() => EditorUtility.RevealInFinder(SecurityStore.FilePath);

    [MenuItem(MenuPaths.SecurityDelete, priority = MenuPaths.SecurityPriority + 2)]
    private static void Delete()
    {
        SecurityStore.Delete();
        Debug.Log($"Security record deleted at {SecurityStore.FilePath}");
    }
}
