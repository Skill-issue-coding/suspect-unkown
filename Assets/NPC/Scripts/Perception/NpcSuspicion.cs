using UnityEngine;

public class NpcSuspicion : MonoBehaviour
{
    private IPerceiver perceiver;

    private void Awake() => perceiver = GetComponent<IPerceiver>();
    private void OnEnable() => perceiver.Perceived += OnPerceived;
    private void OnDisable() => perceiver.Perceived -= OnPerceived;

    private void OnPerceived(Observation observation)
    {
        // react to one tick of seeing the player
    }
}
