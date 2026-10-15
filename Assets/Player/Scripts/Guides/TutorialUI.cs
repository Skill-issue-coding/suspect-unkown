using PrimeTween;
using UnityEngine;

// The controls card at the start of a level: slides and fades in a moment after the level shows, dims each
// control once the player has used it (move, sprint, crouch), and fades out once all three are used or the
// player has been moving for a while, then switches its canvas off. Hides at once when the job ends. Reads
// PlayerInputs every frame, because those are continuous inputs with no event to listen to
public class TutorialUI : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The card, faded in and out as a whole.")]
    [SerializeField] private CanvasGroup card;

    [Tooltip("Row for moving, dimmed once the player has moved.")]
    [SerializeField] private CanvasGroup moveRow;

    [Tooltip("Row for sprinting, dimmed once the player has sprinted.")]
    [SerializeField] private CanvasGroup sprintRow;

    [Tooltip("Row for crouching, dimmed once the player has crouched.")]
    [SerializeField] private CanvasGroup crouchRow;

    [Tooltip("The player's input, watched for each control.")]
    [SerializeField] private PlayerInputs playerInputs;

    [Header("Timing")]
    [Tooltip("Seconds after the level starts before the card comes in, so it arrives after the loading card has gone.")]
    [SerializeField] private float showDelay = 1.2f;

    [Tooltip("Seconds the card takes to fade in or out.")]
    [SerializeField] private float fadeSeconds = 0.5f;

    [Tooltip("Pixels the card slides down as it fades in.")]
    [SerializeField] private float slide = 18f;

    [Tooltip("Opacity of a row once its control has been used, 0 to 1.")]
    [Range(0f, 1f)]
    [SerializeField] private float usedAlpha = 0.35f;

    [Tooltip("Seconds the card stays after the last control is used, so the last dim is seen.")]
    [SerializeField] private float lingerSeconds = 1.5f;

    [Tooltip("Seconds after the first move before the card goes anyway, for players who never sprint or crouch.")]
    [SerializeField] private float hideDelay = 10f;

    private RectTransform cardRect;
    private bool moved, sprinted, crouched, hiding;
    private float sinceFirstMove, allUsedFor;

    private void Awake()
    {
        if (card == null || playerInputs == null)
        {
            Debug.LogWarning($"{name} needs its card and the player's PlayerInputs.", this);
            enabled = false;
            return;
        }
        cardRect = (RectTransform)card.transform;
        card.alpha = 0f;
        card.blocksRaycasts = false; // shown only, never clicked
    }

    private void OnEnable()
    {
        JobOutcome.Completed += Hide;
        JobOutcome.Failed += OnFailed;
    }

    private void OnDisable()
    {
        JobOutcome.Completed -= Hide;
        JobOutcome.Failed -= OnFailed;
    }

    private void Start()
    {
        float restY = cardRect.anchoredPosition.y;
        Tween.Alpha(card, 0f, 1f, fadeSeconds, startDelay: showDelay);
        Tween.UIAnchoredPositionY(cardRect, restY + slide, restY, fadeSeconds, Ease.OutCubic, startDelay: showDelay);
    }

    private void Update()
    {
        if (hiding) return;

        bool moving = playerInputs.move != Vector2.zero;
        if (!moved && moving) moved = Use(moveRow);
        if (!sprinted && moving && playerInputs.sprint) sprinted = Use(sprintRow);
        if (!crouched && playerInputs.crouch) crouched = Use(crouchRow);

        if (moved) sinceFirstMove += Time.deltaTime;
        if (moved && sprinted && crouched) allUsedFor += Time.deltaTime;
        if (allUsedFor >= lingerSeconds || sinceFirstMove >= hideDelay) Hide();
    }

    // Dims the row and reports it as used
    private bool Use(CanvasGroup row)
    {
        if (row != null) Tween.Alpha(row, row.alpha, usedAlpha, 0.3f);
        return true;
    }

    private void OnFailed(FailReason reason) => Hide();

    // Unscaled, because the failure newspaper slows time down
    private void Hide()
    {
        if (hiding || !enabled) return;
        hiding = true;
        Tween.StopAll(card);
        Tween.StopAll(cardRect);
        Tween.Alpha(card, card.alpha, 0f, fadeSeconds, useUnscaledTime: true)
            .OnComplete(this, tutorial => tutorial.gameObject.SetActive(false), warnIfTargetDestroyed: false);
    }
}
