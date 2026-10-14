using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The world-space interaction hint at a reach: the E box follows whoever is using it, and a dotted
// line runs from the box to the target. Active only while shown, so LateUpdate costs nothing otherwise.
// When the target is blocked (a lock above the player's skill) it shows a lock and greys out
public class WorldPrompt : MonoBehaviour
{
    [SerializeField, Tooltip("The canvas's own RectTransform; the plane everything is drawn in")]
    private RectTransform canvas;

    [SerializeField, Tooltip("Fades the whole prompt")]
    private CanvasGroup group;

    [SerializeField, Tooltip("Moved to the follower's position")]
    private RectTransform root;

    [SerializeField, Tooltip("Child of root, pivot (0, 0.5); stretched and turned toward the target")]
    private RectTransform line;

    [SerializeField, Tooltip("Shows the prompt text")]
    private TMP_Text label;

    [SerializeField, Tooltip("Metres above the follower's feet")]
    private Vector3 offset;

    [SerializeField, Tooltip("Seconds to fade in or out")]
    private float fadeDuration = 0.12f;

    [Header("Blocked (lock above the player's skill)")]
    [SerializeField, Tooltip("Shown only while blocked")]
    private GameObject lockIcon;

    [SerializeField, Tooltip("The box behind the label; greyed out while blocked")]
    private Graphic box;

    [SerializeField, Tooltip("Optional mesh behind the box; greyed out while blocked")]
    private Renderer boxBackground;

    [SerializeField, Tooltip("Colour of the box, line and background while blocked")]
    private Color blockedTint = new Color(0.42f, 0.44f, 0.48f, 1f);

    [SerializeField, Tooltip("Label colour while blocked")]
    private Color blockedLabelColor = Color.white;

    [Header("Hold")]
    [SerializeField, Tooltip("Optional. Filled image over the box showing hold progress; doors never fill it")]
    private Image holdFill;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Tween fade;
    private bool visible;
    private Transform follower;
    private Vector3 target;
    private Transform view;

    // The authored look, restored when not blocked
    private Graphic lineGraphic;
    private Color boxColor;
    private Color lineColor;
    private Color labelColor;
    private MaterialPropertyBlock backgroundBlock;

    private void Awake()
    {
        Camera main = Camera.main;
        view = main != null ? main.transform : null;

        if (canvas == null || root == null || line == null || label == null || view == null || group == null)
        {
            Debug.LogWarning($"{name} needs a canvas, root, line, label, group and a main camera", this);
            enabled = false;
            return;
        }

        if (lockIcon == null || box == null)
            Debug.LogWarning($"{name} has no lock icon or box; a blocked prompt will look like a usable one", this);

        lineGraphic = line.GetComponent<Graphic>();
        if (box != null) boxColor = box.color;
        if (lineGraphic != null) lineColor = lineGraphic.color;
        labelColor = label.color;
        if (boxBackground != null) backgroundBlock = new MaterialPropertyBlock();
    }

    // Blocked: the door is locked above the player's Lockpicking level. Shows the lock and greys the rest
    public void Show(Transform follower, Vector3 target, string text, bool blocked)
    {
        this.follower = follower;
        this.target = target;

        // Activate first: the first time, Awake runs here and records the authored colours
        // before SetBlocked changes them
        if (!gameObject.activeSelf)
        {
            group.alpha = 0f;
            gameObject.SetActive(true);
        }
        if (!enabled) return;

        label.text = text;
        SetBlocked(blocked);

        label.text = text;
        SetBlocked(blocked);
        SetProgress(0f);

        if (visible) return;
        visible = true;

        fade.Stop();
        fade = Tween.Alpha(group, 1f, fadeDuration, Ease.OutQuad);
    }

    // Only on Show, which runs on trigger and lockpicking events, never per frame
    private void SetBlocked(bool blocked)
    {
        if (lockIcon != null) lockIcon.SetActive(blocked);
        if (box != null) box.color = blocked ? blockedTint : boxColor;
        if (lineGraphic != null) lineGraphic.color = blocked ? blockedTint : lineColor;
        label.color = blocked ? blockedLabelColor : labelColor;

        if (boxBackground != null)
        {
            // A property block greys this prompt only, without touching the shared material
            if (blocked)
            {
                backgroundBlock.SetColor(BaseColorId, blockedTint);
                boxBackground.SetPropertyBlock(backgroundBlock);
            }
            else boxBackground.SetPropertyBlock(null);
        }
    }

    public void SetProgress(float progress)
    {
        if (holdFill != null) holdFill.fillAmount = progress;
    }

    public void Hide()
    {
        if (!visible) return;
        visible = false;

        fade.Stop();
        fade = Tween.Alpha(group, 0f, fadeDuration, Ease.OutQuad)
            .OnComplete(this, self => self.gameObject.SetActive(false), warnIfTargetDestroyed: false);
    }

    private void LateUpdate()
    {
        if (follower == null) return;

        canvas.rotation = view.rotation;

        Vector3 from = canvas.InverseTransformPoint(follower.position + Vector3.up + offset);
        Vector3 to = canvas.InverseTransformPoint(target);
        from.z = offset.z;
        to.z = 0f;

        root.localPosition = from;

        Vector2 d = to - from;
        line.sizeDelta = new Vector2(d.magnitude, line.sizeDelta.y);

        line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

}
