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
// always, the arrow only while the NPC sees the player.
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

    private NpcPerception perception;
    private float arrowAlpha;
    private NpcSuspicion suspicion;
    private CanvasGroup group;
    private Camera mainCamera;
    private OffscreenIndicatorElement offscreen;
    private bool canRotate;

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

        if (arrowPivot == null)
            Debug.LogWarning($"{name} has no ArrowPivot; indicator rotation disabled.", this);
    }

    private void OnEnable()
    {
        if (suspicion != null)
            suspicion.AlertStateChanged += OnAlertStateChanged;
    }

    private void OnDisable()
    {
        if (suspicion != null)
            suspicion.AlertStateChanged -= OnAlertStateChanged;
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

        ApplyState(suspicion.State);
    }

    private void OnDestroy()
    {
        if (offscreen != null)
            Destroy(offscreen.gameObject);
    }

    private void LateUpdate()
    {
        displayedFill = Mathf.SmoothDamp(displayedFill, suspicion.Suspicion, ref fillVelocity, fillSmoothTime);
        fill.fillAmount = displayedFill;

        transform.rotation = mainCamera.transform.rotation;

        if (canRotate)
            UpdateArrow();

        bool visible = displayedFill > 0.001f || suspicion.State != NpcAlertState.Idle;
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

    private void OnAlertStateChanged(NpcAlertState from, NpcAlertState to) => ApplyState(to);

    private void ApplyState(NpcAlertState state)
    {
        icon.text = state switch
        {
            NpcAlertState.Idle => "",
            NpcAlertState.Curious => "?",
            NpcAlertState.Investigating => "?!",
            NpcAlertState.Alerted => "!",
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };

        Color color = state switch
        {
            NpcAlertState.Idle => idleColor,
            NpcAlertState.Curious => curiousColor,
            NpcAlertState.Investigating => investigatingColor,
            NpcAlertState.Alerted => alertedColor,
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };

        icon.color = color;
        fill.color = color;

        if (offscreen != null)
            offscreen.SetState(icon.text, color);
    }

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
