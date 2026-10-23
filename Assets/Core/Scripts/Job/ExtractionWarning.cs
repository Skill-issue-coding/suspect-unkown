using UnityEngine;
using System;
using TMPro;

// Confirmation popup when the player tries to leave a job with only pocket loot (cash). Bag items
// are saved on exit, but cash is not, so the player confirms before forfeiting it. Shown by
// ExtractionZone; the two buttons call Leave and Stay, which invoke the callbacks it was given.
// Lives on the always-active canvas root, inactive until needed

public class ExtractionWarning : MonoBehaviour
{
    [SerializeField, Tooltip("The panel. Inactive until Show is called")]
    private GameObject screenRoot;
    [SerializeField, Tooltip("The warning text")]
    private TMP_Text message;
    [SerializeField, Tooltip("Warning shown to the player")]
    private string warningText = "You haven't stolen anything for the bag. Leaving now will drop all cash picked up.";

    private Action OnLeave;
    private Action OnStay;

    public static ExtractionWarning Active { get; private set; }

    private void Awake()
    {
        if (screenRoot == null || message == null)
        {
            Debug.Log($"{name} is missing references; ExtractionMarker warning will not be shown", this);
            enabled = false;
            return;
        }
        screenRoot.SetActive(false);
    }

    public void Show(Action OnLeave, Action OnStay)
    {
        this.OnLeave = OnLeave;
        this.OnStay = OnStay;

        message.text = warningText;
        screenRoot.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void Leave()
    {
        screenRoot.SetActive(false);
        Time.timeScale = 1f;
        OnLeave?.Invoke();
    }

    // Connected to the stay button's on click
    public void Stay()
    {
        screenRoot.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        OnStay?.Invoke();

    }
}


