using PrimeTween;
using TMPro;
using UnityEngine;

// The line beside the player (who is always at the centre of the screen) saying what their hands just did:
// "+ LAPTOP" for a taken item, or why they could not take it ("BAG FULL", in red). Fades in, holds, and
// fades out. Hears LootPickup.Taken and Interactable.Refused, so it needs no reference to any pickup or
// to the player. A second line restarts the sequence
public class PickupToast : MonoBehaviour
{
    [SerializeField, Tooltip("Fades the toast in and out")]
    private CanvasGroup group;

    [SerializeField, Tooltip("The announcement line")]
    private TMP_Text label;

    [SerializeField, Tooltip("Seconds to fade in, and again to fade out")]
    private float fadeDuration = 0.2f;

    [SerializeField, Tooltip("Seconds the toast stays fully shown")]
    private float holdSeconds = 2f;

    [SerializeField, Tooltip("Text color for a refusal (\"BAG FULL\"). A taken item keeps the label's authored color")]
    private Color refusedColor = new Color(1f, 0.3f, 0.27f, 1f);

    private Sequence show;
    private Color takenColor;

    private void Awake()
    {
        if (group == null || label == null)
        {
            Debug.LogWarning($"{name} needs a canvas group and a label; assign it in JobHud.prefab", this);
            enabled = false;
            return;
        }

        takenColor = label.color;
        group.alpha = 0f;
    }

    private void OnEnable()
    {
        LootPickup.Taken += OnTaken;
        Interactable.Refused += OnRefused;
    }

    private void OnDisable()
    {
        LootPickup.Taken -= OnTaken;
        Interactable.Refused -= OnRefused;
    }

    private void OnTaken(LootPickup pickup) => Show($"+ {pickup.Item.DisplayName.ToUpperInvariant()}", takenColor);

    // A blocked interactable with nothing to say (a locked door) leaves its greyed prompt to speak
    private void OnRefused(Interactable interactable)
    {
        string reason = interactable.BlockedText;
        if (reason != null) Show(reason.ToUpperInvariant(), refusedColor);
    }

    private void Show(string text, Color color)
    {
        label.text = text;
        label.color = color;

        show.Stop();
        show = Sequence.Create()
            .Chain(Tween.Alpha(group, 1f, fadeDuration, Ease.OutQuad))
            .ChainDelay(holdSeconds)
            .Chain(Tween.Alpha(group, 0f, fadeDuration, Ease.InQuad));
    }
}
