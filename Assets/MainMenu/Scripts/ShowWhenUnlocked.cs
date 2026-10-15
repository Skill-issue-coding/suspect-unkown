using UnityEngine;

// Hides a safehouse prop until the save holds its unlock, so stolen loot appears in the room once the
// job that took it is completed. Reads the save once; the menu reloads after every job
public class ShowWhenUnlocked : MonoBehaviour
{
    [SerializeField, Tooltip("What the save must hold before the prop shows")]
    private ProgressUnlock requires = ProgressUnlock.Shop;

    private void Awake() => gameObject.SetActive(ProgressStore.Load().Has(requires));
}
