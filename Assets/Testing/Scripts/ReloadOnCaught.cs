using UnityEngine;
using UnityEngine.SceneManagement;

// Test bench only: reloads the scene when this guard catches the player. The Phase 3 fail state
// replaces this by listening to the same GuardBehaviour.Caught event.
[RequireComponent(typeof(GuardBehaviour))]
public class ReloadOnCaught : MonoBehaviour
{
    private GuardBehaviour guard;

    private void Awake() => guard = GetComponent<GuardBehaviour>();
    private void OnEnable() => guard.Caught += Reload;
    private void OnDisable() => guard.Caught -= Reload;

    private void Reload() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
}
