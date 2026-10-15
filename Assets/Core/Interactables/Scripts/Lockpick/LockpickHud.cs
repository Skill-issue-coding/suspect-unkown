using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The lockpicking minigame's 2D layer: control hints, a status line and a turn meter, all read from the
// LockpickModel. It teaches without interrupting: the hint for what the player can do right now lights
// up, the status line names the lock's state, and a tip appears only after repeated slips. Draws only;
// the model decides. An overlay canvas of its own inside the scene's PlayerHud prefab; LockpickView finds
// it through Current, so the two need no reference to each other
public class LockpickHud : MonoBehaviour
{
    // The scene's lockpicking HUD. Set in Awake and cleared on destroy, so a reloaded scene never keeps
    // the old one
    public static LockpickHud Current { get; private set; }

    [Header("Parts")]
    [SerializeField, Tooltip("Fades the whole HUD")]
    private CanvasGroup group;

    [SerializeField, Tooltip("Hint row for moving the pick; lit while the pick can move")]
    private CanvasGroup moveHint;

    [SerializeField, Tooltip("Hint row for holding tension; lit while it is held")]
    private CanvasGroup turnHint;

    [SerializeField, Tooltip("One line naming the lock's current state")]
    private TMP_Text status;

    [SerializeField, Tooltip("Vertical filled image: how far the lock has turned")]
    private Image meterFill;

    [SerializeField, Tooltip("Shows the lock level as text")]
    private TMP_Text levelLabel;

    [SerializeField, Tooltip("One pip per lock level, lit up to the current lock's level")]
    private Image[] levelPips;

    [Header("Look")]
    [SerializeField, Tooltip("Color while searching")]
    private Color idleColor = new Color(0.62f, 0.68f, 0.8f, 1f);

    [SerializeField, Tooltip("Color while turning")]
    private Color activeColor = new Color(0.92f, 0.96f, 1f, 1f);

    [SerializeField, Tooltip("Color while straining or after a slip")]
    private Color warningColor = new Color(1f, 0.48f, 0.32f, 1f);

    [SerializeField, Tooltip("Color once the lock is open")]
    private Color successColor = new Color(0.45f, 1f, 0.6f, 1f);

    [SerializeField, Range(0f, 1f), Tooltip("Alpha of a hint row that does nothing right now")]
    private float dimAlpha = 0.3f;

    [SerializeField, Tooltip("Seconds to fade the HUD in or out")]
    private float fadeDuration = 0.15f;

    [SerializeField, Tooltip("Strain pulses per second while the lock binds")]
    private float strainPulseRate = 10f;

    [Header("Text")]
    [SerializeField, Tooltip("Slips before the tip replaces the search line")]
    private int tipAfterSlips = 2;

    [SerializeField, Tooltip("Status while searching, before the tip")]
    private string searchText = "Move the pick, then hold to turn the lock";

    [SerializeField, Tooltip("Status while searching, after enough slips")]
    private string tipText = "Tip: the lock turns furthest near the sweet spot. Release, nudge the pick, try again";

    [SerializeField, Tooltip("Status while tension is held and the lock turns freely")]
    private string turningText = "Turning...";

    [SerializeField, Tooltip("Status while the lock binds")]
    private string strainingText = "Straining - release before the pick slips";

    [SerializeField, Tooltip("Status during the lockout after a slip")]
    private string slippedText = "The pick slipped";

    [SerializeField, Tooltip("Status once the lock is open")]
    private string openText = "Unlocked";

    private LockpickModel model;
    private Tween fade;
    private int slips;
    private string shownMessage;

    private void Awake()
    {
        Current = this;

        if (group == null || moveHint == null || turnHint == null || status == null || meterFill == null || levelLabel == null)
        {
            Debug.LogWarning($"{name} is missing parts; assign them in LockpickHud.prefab", this);
            enabled = false;
        }

        if (group != null) group.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        Detach();
        if (Current == this) Current = null;
    }

    public void Show(LockpickModel lockModel, int lockLevel)
    {
        if (!enabled || lockModel == null) return;

        Detach();
        model = lockModel;
        model.Slipped += OnSlipped;
        slips = 0;
        shownMessage = null;

        levelLabel.text = $"LEVEL {lockLevel}";
        for (int i = 0; i < levelPips.Length; i++)
        {
            if (levelPips[i] != null)
                levelPips[i].color = i < lockLevel ? activeColor : new Color(idleColor.r, idleColor.g, idleColor.b, 0.2f);
        }

        gameObject.SetActive(true);
        fade.Stop();
        fade = Tween.Alpha(group, 1f, fadeDuration, Ease.OutQuad);
    }

    // The last state stays on screen while fading out, so "Unlocked" is still readable as it goes
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
        if (model != null) model.Slipped -= OnSlipped;
        model = null;
    }

    private void OnSlipped() => slips++;

    // Continuous: the meter and the pulse follow the lock every frame. Text changes only on a state change
    private void LateUpdate()
    {
        if (model == null) return;

        bool open = model.IsOpen;
        bool lockedOut = model.IsLockedOut;
        bool straining = model.IsBinding;
        bool turning = model.HasTension;

        string message;
        Color color;
        if (open) { message = openText; color = successColor; }
        else if (lockedOut) { message = slippedText; color = warningColor; }
        else if (straining) { message = strainingText; color = warningColor; }
        else if (turning) { message = turningText; color = activeColor; }
        else { message = slips >= tipAfterSlips ? tipText : searchText; color = idleColor; }

        if (!ReferenceEquals(message, shownMessage))
        {
            shownMessage = message;
            status.text = message;
            status.color = color;
        }

        // The pick moves only without tension, and tension does nothing during a slip lockout
        moveHint.alpha = turning || open ? dimAlpha : 1f;
        turnHint.alpha = lockedOut || open ? dimAlpha : 1f;

        meterFill.fillAmount = model.LockTurn;
        if (open) meterFill.color = successColor;
        else if (lockedOut) meterFill.color = warningColor;
        else if (straining)
        {
            // Pulses faster and harder the closer the pick is to slipping
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * strainPulseRate * (1f + model.Strain));
            meterFill.color = Color.Lerp(activeColor, warningColor, Mathf.Max(model.Strain, pulse * model.Strain));
        }
        else meterFill.color = Color.Lerp(idleColor, activeColor, model.LockTurn);
    }
}
