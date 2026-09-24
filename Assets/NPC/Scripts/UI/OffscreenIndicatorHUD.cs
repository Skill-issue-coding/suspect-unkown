using UnityEngine;

// Screen-space layer for edge-of-screen suspicion indicators. One per scene, on a Screen Space -
// Overlay canvas. The static Instance lets NPCs find it without a scene search.
public class OffscreenIndicatorHUD : MonoBehaviour
{
    [SerializeField] private OffscreenIndicatorElement elementPrefab;

    public static OffscreenIndicatorHUD Instance { get; private set; }

    private void OnEnable()
    {
        if (elementPrefab == null)
        {
            Debug.LogWarning($"{name} has no element prefab; edge indicators disabled.", this);
            return;
        }

        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    public OffscreenIndicatorElement Create() => Instantiate(elementPrefab, transform);
}
