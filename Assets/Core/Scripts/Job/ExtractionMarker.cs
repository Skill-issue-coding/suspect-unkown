using PrimeTween;
using UnityEngine;

// The ground marking at the van's back doors: a frame the size of the zone, grey with a lock while
// locked, pulsing in the accent color once unlocked, and a fill that grows toward the van with the
// zone's progress. Displays only; the zone decides. Re-reads the state on StateChanged; only the fill
// follows progress per frame, and only while it moves. Sprites on the Default layer, not world UI,
// which WorldUiOverlay would draw over the player standing on it
public class ExtractionMarker : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField, Tooltip("The zone this marks")]
    private ExtractionZone zone;

    [SerializeField, Tooltip("Outline the size of the zone. Draw Mode Sliced")]
    private SpriteRenderer frame;

    [SerializeField, Tooltip("Grows along local +Y with progress. Draw Mode Sliced, sized to the full zone")]
    private SpriteRenderer fill;

    [SerializeField, Tooltip("Shown while the zone is locked")]
    private SpriteRenderer lockIcon;

    [Header("Look")]
    [SerializeField, Tooltip("Frame color while locked")]
    private Color lockedColor = new Color(0.93f, 0.91f, 0.86f, 0.3f);

    [SerializeField, Tooltip("Frame color once unlocked; the pulse dips its alpha")]
    private Color unlockedColor = new Color(1f, 0.71f, 0.28f, 1f);

    [SerializeField, Range(0f, 1f), Tooltip("Frame alpha at the low point of the pulse")]
    private float pulseLowAlpha = 0.35f;

    [SerializeField, Tooltip("Seconds for one full pulse, bright to dim and back")]
    private float pulsePeriod = 1.2f;

    private Tween pulse;
    private float fullDepth;
    private Vector3 fillStart;
    private float shownProgress = -1f;

    private void Awake()
    {
        if (zone == null || frame == null || fill == null || lockIcon == null)
        {
            Debug.LogWarning($"{name} needs its zone, frame, fill and lock icon", this);
            enabled = false;
            return;
        }

        fullDepth = fill.size.y;
        fillStart = fill.transform.localPosition - new Vector3(0f, fullDepth * 0.5f, 0f);
    }

    private void OnEnable()
    {
        if (zone != null) zone.StateChanged += ShowState;
    }

    private void OnDisable()
    {
        if (zone != null) zone.StateChanged -= ShowState;
        pulse.Stop();
    }

    private void Start() => ShowState();

    private void LateUpdate()
    {
        float progress = zone.Progress;
        if (progress == shownProgress) return;
        shownProgress = progress;

        float depth = fullDepth * progress;
        fill.enabled = depth > 0f;
        fill.size = new Vector2(fill.size.x, depth);
        fill.transform.localPosition = fillStart + new Vector3(0f, depth * 0.5f, 0f);
    }

    private void ShowState()
    {
        bool unlocked = zone.IsUnlocked;
        lockIcon.enabled = !unlocked;

        pulse.Stop();
        frame.color = unlocked ? unlockedColor : lockedColor;
        if (!unlocked) return;

        pulse = Tween.Alpha(frame, pulseLowAlpha, pulsePeriod * 0.5f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);
    }
}
