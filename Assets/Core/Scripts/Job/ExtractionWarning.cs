using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The question asked when the player reaches the end of the extraction zone without a bag item: leaving
// now saves nothing, so they confirm or go back. ExtractionZone opens it and passes what each answer
// does; the buttons call Continue and Cancel, and Escape cancels too. The world freezes while it is open.
// Shown and Hidden let the player side take and give back its controls without this knowing the player.
// Lives on an always-active canvas in PlayerHud, with its panel inactive until needed
public class ExtractionWarning : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Backdrop and panel. Inactive until Show is called")]
    private GameObject screenRoot;

    [SerializeField, Tooltip("Faded in when shown")]
    private CanvasGroup group;

    [SerializeField, Tooltip("Receives the message text")]
    private TMP_Text message;

    [SerializeField, Tooltip("Selected when shown, so keyboard and gamepad land on the safe answer")]
    private Button cancelButton;

    [Header("Text")]
    [SerializeField, Tooltip("Body of the warning"), TextArea(2, 4)]
    private string warningText = "Looks like you didn't grab any bag items. Leaving now means you walk away with nothing.";

    [Header("Look")]
    [SerializeField, Tooltip("Seconds, unscaled, to fade in")]
    private float fadeDuration = 0.2f;

    private Action onContinue;
    private Action onCancel;
    private InputAction escapeAction;
    private float timeScaleBefore = 1f;
    private Tween fade;

    public static ExtractionWarning Active { get; private set; }
    public bool IsOpen { get; private set; }

    public static event Action Shown;
    public static event Action Hidden;

    private void Awake()
    {
        if (screenRoot == null || group == null || message == null || cancelButton == null)
        {
            Debug.LogWarning($"{name} is missing references; leaving empty-handed will not ask first", this);
            enabled = false;
            return;
        }

        screenRoot.SetActive(false);
        escapeAction = new InputAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
    }

    private void OnEnable()
    {
        if (escapeAction == null) return;
        Active = this;
        escapeAction.performed += OnEscape;
    }

    private void OnDisable()
    {
        if (escapeAction == null) return;
        if (Active == this) Active = null;
        escapeAction.performed -= OnEscape;
        escapeAction.Disable();
        fade.Stop();
    }

    private void OnDestroy() => escapeAction?.Dispose();

    public void Show(Action continueAction, Action cancelAction)
    {
        if (IsOpen) return;
        IsOpen = true;
        onContinue = continueAction;
        onCancel = cancelAction;

        message.text = warningText;
        screenRoot.SetActive(true);
        group.alpha = 0f;
        fade = Tween.Alpha(group, 1f, fadeDuration, useUnscaledTime: true);

        timeScaleBefore = Time.timeScale;
        Time.timeScale = 0f;
        escapeAction.Enable();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);

        Shown?.Invoke();
    }

    // Connected to ContinueButton's On Click
    public void Continue() => Answer(onContinue);

    // Connected to CancelButton's On Click
    public void Cancel() => Answer(onCancel);

    private void OnEscape(InputAction.CallbackContext context) => Cancel();

    // The answer runs before Hidden, so a Continue that ends the job has ended it by the time listeners
    // decide whether to hand the controls back
    private void Answer(Action answer)
    {
        if (!IsOpen) return;
        IsOpen = false;
        onContinue = null;
        onCancel = null;

        fade.Stop();
        escapeAction.Disable();
        screenRoot.SetActive(false);
        Time.timeScale = timeScaleBefore;

        answer?.Invoke();
        Hidden?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Active = null;
        Shown = null;
        Hidden = null;
    }
}
