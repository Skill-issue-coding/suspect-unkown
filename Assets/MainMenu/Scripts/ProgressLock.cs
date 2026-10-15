using TMPro;
using UnityEngine;

// Locks a menu button until the save holds an unlock: the button shows the lock (MenuButtonFx.SetLocked)
// and its subtitle says how to open it. Reads the save once at start; the menu reloads after every job
[RequireComponent(typeof(MenuButtonFx))]
public class ProgressLock : MonoBehaviour
{
    [SerializeField, Tooltip("What the save must hold before the button opens")]
    private ProgressUnlock requires = ProgressUnlock.Shop;

    [SerializeField, Tooltip("Subtitle shown in place of the button's own, while locked. Empty: the subtitle stays")]
    private TMP_Text subtitle;

    [SerializeField, Tooltip("How to unlock it, shown in the subtitle")]
    private string lockedHint = "Steal a laptop to unlock";

    private void Start()
    {
        bool open = ProgressStore.Load().Has(requires);
        GetComponent<MenuButtonFx>().SetLocked(!open);

        if (!open && subtitle != null)
            subtitle.text = lockedHint;
    }
}
