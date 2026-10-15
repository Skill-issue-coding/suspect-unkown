using UnityEngine;
using UnityEngine.SceneManagement;

// The one way to change scene: covers the screen with a loading card, loads the next scene in the background,
// switches once it is ready, then uncovers (SceneTransitionView does the work). Static, so any caller can use
// it without a reference. The card is a prefab loaded from Resources the first time and kept across scenes,
// because it has to outlive the scene it covers. It carries no game state: jobs and progress still travel
// through the save
public static class SceneTransition
{
    private const string PrefabPath = "SceneTransition";

    private static SceneTransitionView view;

    public static bool IsLoading => view != null && view.IsLoading;

    // Ignored while a transition runs, so a double click cannot start two loads
    public static void Load(string scene)
    {
        if (IsLoading) return;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning($"SceneTransition: scene '{scene}' is not in the build scene list");
            return;
        }

        if (view == null)
        {
            SceneTransitionView prefab = Resources.Load<SceneTransitionView>(PrefabPath);
            if (prefab == null)
            {
                // Still change scene, just without the card
                Debug.LogWarning($"SceneTransition: no Resources/{PrefabPath} prefab, loading without a transition");
                SceneManager.LoadScene(scene);
                return;
            }
            view = Object.Instantiate(prefab);
        }
        view.Begin(scene);
    }

    // Static fields survive play-mode entry when domain reload is off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => view = null;
}
