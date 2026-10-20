using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The glass cutter minigame's 2D layer: control hints, a status line and a cut meter, all read from the
// GlassCutterModel. Laid out like LockpickHud so the two minigames read the same: the hint for what the
// player can do right now lights up, the status line names the cut's state, and a tip appears only after
// repeated misaligned strains. Draws only; the model decides. An overlay canvas of its own inside
// Minigames.prefab, beside the GlassCutterView that shows and hides it
public class GlassCutterHud : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField, Tooltip("Fades the whole HUD")]
    private CanvasGroup group;

    [SerializeField, Tooltip("Hint row for aiming along the line; lit until the cut is done")]
    private CanvasGroup aimHint;

    [SerializeField, Tooltip("Hint row for holding to cut; lit while the aim is on the line")]
    private CanvasGroup cutHint;

    [SerializeField, Tooltip("One line naming the cut's current state")]
    private TMP_Text status;

    [SerializeField, Tooltip("Vertical filled image: how much of the circle is cut")]
    private Image meterFill;

    [Header("Look")]
    [SerializeField, Tooltip("Color while aiming")]
    private Color idleColor = new Color(0.62f, 0.68f, 0.8f, 1f);

    [SerializeField, Tooltip("Color while on the line or cutting")]
    private Color activeColor = new Color(0.92f, 0.96f, 1f, 1f);

    [SerializeField, Tooltip("Color while straining off the line")]
    private Color warningColor = new Color(1f, 0.48f, 0.32f, 1f);

    [SerializeField, Tooltip("Color once the circle is cut")]
    private Color successColor = new Color(0.45f, 1f, 0.6f, 1f);

    [SerializeField, Range(0f, 1f), Tooltip("Alpha of a hint row that does nothing right now")]
    private float dimAlpha = 0.3f;

    [SerializeField, Tooltip("Seconds to fade the HUD in or out")]
    private float fadeDuration = 0.15f;

    [SerializeField, Tooltip("Strain pulses per second while cutting off the line")]
    private float strainPulseRate = 10f;

    [Header("Text")]
    [SerializeField, Tooltip("Misaligned strains before the tip replaces the aim line")]
    private int tipAfterStrains = 2;

    [SerializeField, Tooltip("Status while aiming off the line, before the tip")]
    private string aimText = "Point along the dotted line, then hold to cut";

    [SerializeField, Tooltip("Status while aiming off the line, after enough strains")]
    private string tipText = "Tip: point ahead of the wheel, the way the cut is going";

    [SerializeField, Tooltip("Status while the aim is on the line without tension")]
    private string alignedText = "On the line - hold to cut";

    [SerializeField, Tooltip("Status while tension is held on the line")]
    private string cuttingText = "Cutting...";

    [SerializeField, Tooltip("Status while tension is held off the line")]
    private string strainingText = "Off the line - release and aim along it";

    [SerializeField, Tooltip("Status once the circle is cut")]
    private string doneText = "Cut through";

    private GlassCutterModel model;
    private Tween fade;
    private int strains;
    private string shownMessage;

    private void Awake()
    {
        if (group == null || aimHint == null || cutHint == null || status == null || meterFill == null)
        {
            Debug.LogWarning($"{name} is missing parts; assign them in GlassCutterHud.prefab", this);
            enabled = false;
        }

        if (group != null) group.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        Detach();
    }

    public void Show(GlassCutterModel cutterModel)
    {
        if (!enabled || cutterModel == null) return;

        Detach();
        model = cutterModel;
        model.Misaligned += OnMisaligned;
        strains = 0;
        shownMessage = null;

        gameObject.SetActive(true);
        fade.Stop();
        fade = Tween.Alpha(group, 1f, fadeDuration, Ease.OutQuad);
    }

    // The last state stays on screen while fading out, so "Cut through" is still readable as it goes
    public void Hide()
    {
        Detach();
        if (!enabled || !gameObject.activeSelf) return;

        fade.Stop();
        fade = Tween.Alpha(group, 0f, fadeDuration, Ease.OutQuad)
            .OnComplete(this, self => self.gameObject.SetActive(false), warnIfTargetDestroyed: false);
    }

    private void Detach()
    {
        if (model != null) model.Misaligned -= OnMisaligned;
        model = null;
    }

    private void OnMisaligned() => strains++;

    // Continuous: the meter and the pulse follow the cut every frame. Text changes only on a state change
    private void LateUpdate()
    {
        if (model == null) return;

        bool done = model.IsComplete;
        bool straining = model.IsStraining;
        bool aligned = model.IsAligned;
        bool cutting = aligned && model.HasTension;

        string message;
        Color color;
        if (done) { message = doneText; color = successColor; }
        else if (straining) { message = strainingText; color = warningColor; }
        else if (cutting) { message = cuttingText; color = activeColor; }
        else if (aligned) { message = alignedText; color = activeColor; }
        else { message = strains >= tipAfterStrains ? tipText : aimText; color = idleColor; }

        if (!ReferenceEquals(message, shownMessage))
        {
            shownMessage = message;
            status.text = message;
            status.color = color;
        }

        // The aim always matters until the cut is done; holding only cuts while the aim is on the line
        aimHint.alpha = done ? dimAlpha : 1f;
        cutHint.alpha = aligned && !done ? 1f : dimAlpha;

        meterFill.fillAmount = model.Progress;
        if (done) meterFill.color = successColor;
        else if (straining)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * strainPulseRate);
            meterFill.color = Color.Lerp(activeColor, warningColor, pulse);
        }
        else meterFill.color = Color.Lerp(idleColor, activeColor, model.Progress);
    }
}
