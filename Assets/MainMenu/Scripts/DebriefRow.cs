using PrimeTween;
using TMPro;
using UnityEngine;

// One row on the debrief board: a label, a detail and an amount on the right, inside a Body that slides and
// fades in. The row itself is the slot a layout group (the take's scroll list) may own, so tweens only ever
// move the Body, whose resting place is x 0. Finds its parts by child name, so the board's rows need no wiring
public class DebriefRow : MonoBehaviour
{
    private bool initialised;
    private RectTransform body;
    private CanvasGroup group;
    private TMP_Text label, detail, amount;

    private void Awake() => Init();

    // Also run from every public method: the board fills rows cloned from an inactive template, whose Awake has
    // not run yet. Guarded so the lookups happen once
    private void Init()
    {
        if (initialised) return;
        initialised = true;

        body = transform.Find("Body") as RectTransform;
        if (body == null)
        {
            Debug.LogWarning($"{name} has no Body child; the row stays blank", this);
            return;
        }
        group = body.GetComponent<CanvasGroup>();
        label = Child("Label");
        detail = Child("Detail");
        amount = Child("Amount");
    }

    public void Set(string labelText, string detailText, string amountText)
    {
        Init();
        if (label != null) label.text = labelText;
        if (detail != null) detail.text = detailText;
        if (amount != null) amount.text = amountText;
    }

    public void Hide()
    {
        Init();
        if (group == null) return;
        Tween.StopAll(group);
        group.alpha = 0f;
    }

    // Slides in from the left with a small overshoot, fading up over the first part of the slide
    public void Reveal(float slide, float seconds)
    {
        Init();
        if (body == null) return;
        if (group != null) Tween.Alpha(group, 0f, 1f, seconds * 0.6f);
        Tween.UIAnchoredPositionX(body, -slide, 0f, seconds, Ease.OutBack);
    }

    // Jumps to the end of a reveal. Stops the tweens first, so a skipped reveal cannot move the row afterwards
    public void ShowNow()
    {
        Init();
        if (body == null) return;
        Tween.StopAll(body);
        body.anchoredPosition = new Vector2(0f, body.anchoredPosition.y);
        if (group == null) return;
        Tween.StopAll(group);
        group.alpha = 1f;
    }

    private TMP_Text Child(string childName)
    {
        Transform child = body.Find(childName);
        TMP_Text text = child != null ? child.GetComponent<TMP_Text>() : null;
        if (text == null) Debug.LogWarning($"{name} has no text child named Body/{childName}", this);
        return text;
    }
}
