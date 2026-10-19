using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// In-world suspicion telegraph (concept 6.5). Hidden at zero, so a calm
// room shows nothing. Icon and color follow AlertStateChanged; the fill
// follows Suspicion every frame. Billboards by copying the fixed camera's
// rotation. The arrow rolls in the screen plane toward the NPC's last stimulus 
// (NpcSuspicion.LastStimulus): what it last saw, heard or noticed, never the live
// player, so it does not leak where the player really is. Off screen, an
// OffscreenIndicatorElement on the HUD takes over at the screen edge: the symbol
// always, the arrow only while the NPC sees the player. For civilians, what they
// are doing wins over the alert symbol (thriftshop-level.md C6b): while deliberating,
// the icon of the outcome they lean toward in orange, with the window's progress as
// the fill (a lean toward a call also starts the HUD ring); then the cower or the walk
// away, or the phone pulsing red and full, since nothing stops a call once it is under
// way. Customers, staff and the apartment resident report their activity through
// ICivilianActivity. Every symbol is an icon from one TMP sprite asset: an eye when
// curious; for guards a flashlight when investigating and handcuffs when alerted; for
// civilians a staring eye from investigating up.
[RequireComponent(typeof(CanvasGroup))]
public class NpcSuspicionIndicator : MonoBehaviour
{
    [SerializeField] private Image fill;
    [SerializeField] private TMP_Text icon;
    [SerializeField] private RectTransform arrowPivot;

    [Header("State colors")]
    [SerializeField] private Color idleColor = Color.gray;
    [SerializeField] private Color curiousColor = Color.yellow;
    [SerializeField] private Color investigatingColor = new Color(1f, 0.55f, 0f);
    [SerializeField] private Color alertedColor = Color.red;

    [Header("Smoothing")]
    [SerializeField] private float fillSmoothTime = 0.1f;
    [SerializeField] private float turnSmoothTime = 0.12f;
    [SerializeField] private float fadeSpeed = 4f;

    [Header("Offscreen")]
    [Tooltip("Pixels between the edge indicator and the screen border")]
    [SerializeField] private float edgeMargin = 60f;

    [Tooltip("How fast the edge arrow fades in and out as the NPC gains or loses sight of the player, per second")]
    [SerializeField] private float arrowFadeSpeed = 6f;

    [Tooltip("Viewport fraction inside the screen edge where the world indicator hands over to the edge one")]
    [SerializeField] private float onScreenInset = 0.02f;

    [Header("Reactions (civilians only)")]
    [SerializeField, Tooltip("Icon color while deciding; the icon shows the outcome they lean toward")]
    private Color deliberatingColor = new Color(1f, 0.55f, 0f);

    [SerializeField, Tooltip("Icon color once the call is under way")]
    private Color callingColor = Color.red;

    [SerializeField, Min(0f), Tooltip("Pulses per second while the call is under way, as the HUD ring")]
    private float callingPulseRate = 4f;

    [SerializeField, Tooltip("Icon color while scared and blind")]
    private Color scaredColor = new Color(0.65f, 0.75f, 1f);

    [SerializeField, Tooltip("Icon color while walking away")]
    private Color avoidingColor = Color.gray;

    // Sprite names in ReactionIcons.asset, which holds every indicator icon; tint=1 lets the text color tint them
    private const string PhoneIcon = "<sprite name=\"Phone\" tint=1>";
    private const string ScaredIcon = "<sprite name=\"Scared\" tint=1>";
    private const string AvoidIcon = "<sprite name=\"Avoid\" tint=1>";
    private const string EyeIcon = "<sprite name=\"Eye\" tint=1>";
    private const string StareIcon = "<sprite name=\"Stare\" tint=1>";
    private const string FlashlightIcon = "<sprite name=\"Flashlight\" tint=1>";
    private const string HandcuffsIcon = "<sprite name=\"Handcuffs\" tint=1>";

    // Civilians and the resident only: null on guards, who get the guard icons
    private ICivilianActivity civilian;
    private NpcDecisionWindow window;

    private NpcPerception perception;
    private float arrowAlpha;
    private NpcSuspicion suspicion;
    private CanvasGroup group;
    private Camera mainCamera;
    private OffscreenIndicatorElement offscreen;
    private bool canRotate;

    // Set by Refresh, read every frame: is a reaction shown, and does the fill follow the window or the call
    private bool showingReaction;
    private bool fillFromWindow;
    private bool fillFromCall;

    private float displayedFill;
    private float fillVelocity;
    private float currentAngle;
    private float angleVelocity;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();

        suspicion = GetComponentInParent<NpcSuspicion>();
        if (suspicion == null)
        {
            Debug.LogWarning($"{name} has no NpcSuspicion; NpcSuspicionIndicator disabled.", this);
            enabled = false;
            return;
        }

        perception = GetComponentInParent<NpcPerception>();

        if (fill == null || icon == null)
        {
            Debug.LogWarning($"{name} is missing its fill or icon reference; NpcSuspicionIndicator disabled.", this);
            enabled = false;
            return;
        }

        civilian = GetComponentInParent<ICivilianActivity>();
        window = civilian != null ? GetComponentInParent<NpcDecisionWindow>() : null;

        // The arrow turns toward suspicion.LastStimulus, so the pivot is all it needs
        canRotate = arrowPivot != null;
        if (!canRotate)
            Debug.LogWarning($"{name} has no ArrowPivot; indicator rotation disabled.", this);
    }

    private void OnEnable()
    {
        if (suspicion != null)
            suspicion.AlertStateChanged += OnAlertStateChanged;

        if (civilian != null)
            civilian.ActivityChanged += OnActivityChanged;
    }

    private void OnDisable()
    {
        if (suspicion != null)
            suspicion.AlertStateChanged -= OnAlertStateChanged;

        if (civilian != null)
            civilian.ActivityChanged -= OnActivityChanged;
    }

    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning($"{name} found no main camera; indicator disabled.", this);
            enabled = false;
            return;
        }

        if (OffscreenIndicatorHUD.Instance != null)
            offscreen = OffscreenIndicatorHUD.Instance.Create();

        Refresh();
    }

    private void OnDestroy()
    {
        if (offscreen != null)
            Destroy(offscreen.gameObject);
    }

    private void LateUpdate()
    {
        // While deliberating the fill is the window, how long until they act; during a call it sits full, as
        // the HUD ring does; otherwise suspicion
        float target = fillFromWindow ? window.Progress : fillFromCall ? 1f : suspicion.Suspicion;
        displayedFill = Mathf.SmoothDamp(displayedFill, target, ref fillVelocity, fillSmoothTime);
        fill.fillAmount = displayedFill;

        if (fillFromCall) PulseCall();

        transform.rotation = mainCamera.transform.rotation;

        if (canRotate)
            UpdateArrow();

        bool visible = showingReaction || displayedFill > 0.001f || suspicion.State != NpcAlertState.Idle;
        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, fadeSpeed * Time.deltaTime);

        bool seesPlayer = perception != null && perception.TryGetLatest(out _);
        arrowAlpha = Mathf.MoveTowards(arrowAlpha, seesPlayer ? 1f : 0f, arrowFadeSpeed * Time.deltaTime);

        UpdateOffscreen();
    }

    private void UpdateArrow()
    {
        if (!suspicion.HasStimulus) return;

        Vector3 toTarget = suspicion.LastStimulus - transform.position;

        float x = Vector3.Dot(toTarget, mainCamera.transform.right);
        float y = Vector3.Dot(toTarget, mainCamera.transform.up);
        float targetAngle = -Mathf.Atan2(x, y) * Mathf.Rad2Deg;

        currentAngle = Mathf.SmoothDampAngle(currentAngle, targetAngle, ref angleVelocity, turnSmoothTime);
        arrowPivot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
    }

    // Both refresh the whole icon: a scared civilian's suspicion can fall to Idle while they still cower,
    // and the reaction must win over the alert symbol then too
    private void OnAlertStateChanged(NpcAlertState from, NpcAlertState to) => Refresh();

    private void OnActivityChanged(CivilianActivity from, CivilianActivity to) => Refresh();

    // Guards look into it with a flashlight and come to arrest. A civilian never investigates or arrests:
    // from Investigating up they stand and stare, and an Alerted civilian outside its window (one that
    // drained) is no alarm, so it stays the orange stare instead of turning red
    private string AlertIcon(NpcAlertState state) => state switch
    {
        NpcAlertState.Idle => "",
        NpcAlertState.Curious => EyeIcon,
        NpcAlertState.Investigating => civilian != null ? StareIcon : FlashlightIcon,
        NpcAlertState.Alerted => civilian != null ? StareIcon : HandcuffsIcon,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    private Color AlertColor(NpcAlertState state) => state switch
    {
        NpcAlertState.Idle => idleColor,
        NpcAlertState.Curious => curiousColor,
        NpcAlertState.Investigating => investigatingColor,
        NpcAlertState.Alerted => civilian != null ? investigatingColor : alertedColor,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    // The one place the icon is chosen: a civilian's reaction wins over the alert symbol
    private void Refresh()
    {
        showingReaction = TryReactionIcon(out string text, out Color color);
        fillFromWindow = showingReaction && civilian.Activity == CivilianActivity.Deliberating;
        fillFromCall = showingReaction && civilian.Activity == CivilianActivity.Calling;
        if (!showingReaction)
        {
            text = AlertIcon(suspicion.State);
            color = AlertColor(suspicion.State);
        }

        icon.text = text;
        icon.color = color;
        fill.color = color;
        if (offscreen != null) offscreen.SetState(text, color);
    }

    // While deliberating, the outcome the window sampled when it opened: the lean (thriftshop 2.4)
    private bool TryReactionIcon(out string text, out Color color)
    {
        text = null;
        color = default;
        if (civilian == null) return false;

        switch (civilian.Activity)
        {
            // Orange while the player can still hide (the window drains); red only once the call is under way
            case CivilianActivity.Deliberating: text = OutcomeIcon(window.PendingOutcome); color = deliberatingColor; break;
            case CivilianActivity.Calling: text = PhoneIcon; color = callingColor; break;
            case CivilianActivity.Scared: text = ScaredIcon; color = scaredColor; break;
            case CivilianActivity.Avoiding: text = AvoidIcon; color = avoidingColor; break;
        }
        return text != null;
    }

    // Only while the call is under way, which lasts callSeconds: it tells "too late" apart from the red lean
    private void PulseCall()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * callingPulseRate * Mathf.PI * 2f);
        Color color = Color.Lerp(callingColor, Color.white, 0.35f * pulse);
        icon.color = color;
        fill.color = color;
        if (offscreen != null) offscreen.SetState(icon.text, color);
    }

    private static string OutcomeIcon(CivilianActivity outcome) => outcome switch
    {
        CivilianActivity.Calling => PhoneIcon,
        CivilianActivity.Scared => ScaredIcon,
        CivilianActivity.Avoiding => AvoidIcon,
        _ => null,
    };

    private void UpdateOffscreen()
    {
        if (offscreen == null) return;

        Vector3 viewport = mainCamera.WorldToViewportPoint(transform.position);

        // Behind the camera the projection comes out mirrored; flip it so the arrow points the right way.
        bool behind = viewport.z < 0f;
        if (behind)
        {
            viewport.x = 1f - viewport.x;
            viewport.y = 1f - viewport.y;
        }

        bool onScreen = !behind
            && viewport.x > onScreenInset && viewport.x < 1f - onScreenInset
            && viewport.y > onScreenInset && viewport.y < 1f - onScreenInset;

        if (onScreen)
        {
            offscreen.Place(Vector2.zero, 0f, displayedFill, 0f, arrowAlpha);
            return;
        }

        // Push the centre-to-NPC direction out until it meets the screen rectangle, inset by edgeMargin.
        // A zero axis divides to Infinity, and Min then picks the other axis.
        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 direction = new Vector2(viewport.x * Screen.width, viewport.y * Screen.height) - center;
        Vector2 halfExtents = center - Vector2.one * edgeMargin;

        float scale = Mathf.Min(halfExtents.x / Mathf.Abs(direction.x), halfExtents.y / Mathf.Abs(direction.y));
        Vector2 edge = center + direction * scale;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        offscreen.Place(edge, angle, displayedFill, group.alpha, arrowAlpha);
    }
}
