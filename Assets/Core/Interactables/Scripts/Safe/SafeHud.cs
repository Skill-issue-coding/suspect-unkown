using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The safe-cracking minigame's 2D layer, laid out like LockpickHud: control hints, a status line that
// says which number to find and when it is found, a large readout that jumps on the right number of the number under the marker, a test meter,
// and one pip per number already set. Draws only; the model decides. An overlay canvas of its own inside
// Minigames.prefab, beside the SafeView that shows and hides it
public class SafeHud : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField, Tooltip("Fades the whole HUD")]
    private CanvasGroup group;

    [SerializeField, Tooltip("Hint row for turning the dial; lit while the dial can turn")]
    private CanvasGroup turnHint;

    [SerializeField, Tooltip("Hint row for holding tension to test; lit while it can be held")]
    private CanvasGroup testHint;

    [SerializeField, Tooltip("One line naming what to do next or the dial's current state")]
    private TMP_Text status;

    [SerializeField, Tooltip("The number under the marker")]
    private TMP_Text numberLabel;

    [SerializeField, Tooltip("Vertical filled image: how far the current test has got")]
    private Image meterFill;

    [SerializeField, Tooltip("One pip per number in the combination, lit once that number is set")]
    private Image[] numberPips;

    [Header("Look")]
    [SerializeField, Tooltip("Color while searching")]
    private Color idleColor = new Color(0.62f, 0.68f, 0.8f, 1f);

    [SerializeField, Tooltip("Color while testing, and of the readout while the dial catches")]
    private Color activeColor = new Color(0.92f, 0.96f, 1f, 1f);

    [SerializeField, Tooltip("Color while straining, after a slip, or turning the wrong way")]
    private Color warningColor = new Color(1f, 0.48f, 0.32f, 1f);

    [SerializeField, Tooltip("Color of set numbers and once the safe is open")]
    private Color successColor = new Color(0.45f, 1f, 0.6f, 1f);

    [SerializeField, Range(0f, 1f), Tooltip("Alpha of a hint row that does nothing right now")]
    private float dimAlpha = 0.3f;

    [SerializeField, Tooltip("Seconds to fade the HUD in or out")]
    private float fadeDuration = 0.15f;

    [SerializeField, Tooltip("Strain pulses per second while a test binds")]
    private float strainPulseRate = 10f;

    [Header("Text")]
    [SerializeField, Tooltip("Status while searching for each number; {0} is the number's place (first, second...)")]
    private string findFormat = "Turn the dial to find the {0} number";

    [SerializeField, Tooltip("Status while the next number is under the marker")]
    private string onNumberText = "That's it - hold to test";

    [SerializeField, Tooltip("Scale punch on the readout when the dial catches on the right number")]
    private float catchPunch = 0.3f;

    [SerializeField, Tooltip("Status while tension is held and the test goes through")]
    private string testingText = "Testing...";

    [SerializeField, Tooltip("Status while a test binds")]
    private string strainingText = "Straining - release before the dial slips";

    [SerializeField, Tooltip("Status during the lockout after a slip")]
    private string slippedText = "The dial slipped";

    [SerializeField, Tooltip("Status once the safe is open")]
    private string openText = "Open";

    [SerializeField, Tooltip("Places of the numbers, used in the status line")]
    private string[] places = { "first", "second", "third", "fourth", "fifth" };

    private SafeModel model;
    private Tween fade;
    private string shownMessage;
    private int shownNumber = -1;

    // Built once per Show so the per-frame path never builds strings
    private string[] findTexts;
    private string[] numberTexts;
    private bool wasOnNumber;
    private Tween catchTween;

    private void Awake()
    {
        if (group == null || turnHint == null || testHint == null || status == null || numberLabel == null || meterFill == null)
        {
            Debug.LogWarning($"{name} is missing parts; assign them in SafeHud.prefab", this);
            enabled = false;
        }

        if (group != null) group.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        model = null;
    }

    public void Show(SafeModel safeModel)
    {
        if (!enabled || safeModel == null) return;

        model = safeModel;
        shownMessage = null;
        shownNumber = -1;
        wasOnNumber = false;
        BuildTexts();

        if (numberPips.Length < model.CombinationLength)
            Debug.LogWarning($"{name} has {numberPips.Length} pips for a {model.CombinationLength}-number combination", this);

        gameObject.SetActive(true);
        fade.Stop();
        fade = Tween.Alpha(group, 1f, fadeDuration, Ease.OutQuad);
    }

    // The last state stays on screen while fading out, so "Open" is still readable as it goes
    public void Hide()
    {
        model = null;
        if (!enabled || !gameObject.activeSelf) return;

        fade.Stop();
        fade = Tween.Alpha(group, 0f, fadeDuration, Ease.OutQuad)
            .OnComplete(this, self => self.gameObject.SetActive(false), warnIfTargetDestroyed: false);
    }

    private void BuildTexts()
    {
        int count = model.CombinationLength;
        findTexts = new string[count];
        for (int i = 0; i < count; i++)
        {
            string place = i < places.Length ? places[i] : (i + 1).ToString();
            findTexts[i] = string.Format(findFormat, place);
        }

        if (numberTexts == null || numberTexts.Length != model.DialNumbers)
        {
            numberTexts = new string[model.DialNumbers];
            for (int n = 0; n < numberTexts.Length; n++)
                numberTexts[n] = n.ToString("00");
        }
    }

    // Continuous: the readout, meter and pulse follow the dial every frame. Text changes only on a change
    private void LateUpdate()
    {
        if (model == null) return;

        bool open = model.IsOpen;
        bool lockedOut = model.IsLockedOut;
        bool straining = model.IsBinding;
        bool testing = model.HasTension;
        bool onNumber = model.OnNumber;
        int next = Mathf.Min(model.NumbersSet, model.CombinationLength - 1);

        string message;
        Color color;
        if (open) { message = openText; color = successColor; }
        else if (lockedOut) { message = slippedText; color = warningColor; }
        else if (straining) { message = strainingText; color = warningColor; }
        else if (testing) { message = testingText; color = activeColor; }
        else if (onNumber) { message = onNumberText; color = successColor; }
        else { message = findTexts[next]; color = idleColor; }

        if (!ReferenceEquals(message, shownMessage))
        {
            shownMessage = message;
            status.text = message;
            status.color = color;
        }

        int number = model.Number;
        if (number != shownNumber)
        {
            shownNumber = number;
            numberLabel.text = numberTexts[number];
        }
        numberLabel.color = open || onNumber ? successColor : idleColor;

        // The readout jumps with the dial, once as the right number arrives
        if (onNumber && !wasOnNumber)
        {
            catchTween.Complete();
            catchTween = Tween.PunchScale(numberLabel.transform, Vector3.one * catchPunch, duration: 0.25f, frequency: 6);
        }
        wasOnNumber = onNumber;

        for (int i = 0; i < numberPips.Length; i++)
        {
            if (numberPips[i] == null) continue;

            bool used = i < model.CombinationLength;
            numberPips[i].enabled = used;
            if (used)
                numberPips[i].color = i < model.NumbersSet ? successColor : new Color(idleColor.r, idleColor.g, idleColor.b, 0.2f);
        }

        // The dial turns only without tension, and tension does nothing during a slip lockout
        turnHint.alpha = testing || open ? dimAlpha : 1f;
        testHint.alpha = lockedOut || open ? dimAlpha : 1f;

        meterFill.fillAmount = open ? 1f : model.TestProgress;
        if (open) meterFill.color = successColor;
        else if (lockedOut) meterFill.color = warningColor;
        else if (straining)
        {
            // Pulses faster and harder the closer the dial is to slipping
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * strainPulseRate * (1f + model.Strain));
            meterFill.color = Color.Lerp(activeColor, warningColor, Mathf.Max(model.Strain, pulse * model.Strain));
        }
        else meterFill.color = Color.Lerp(idleColor, activeColor, model.TestProgress);
    }
}
