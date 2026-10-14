using PrimeTween;
using TMPro;
using UnityEngine;

// Announces each taken item above the carry panel: "+ LAPTOP" fades in, holds, and fades out. Hears
// LootPickup.Taken, so it needs no reference to any pickup. A second pickup restarts the sequence
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

    private Sequence show;

    private void Awake()
    {
        if (group == null || label == null)
        {
            Debug.LogWarning($"{name} needs a canvas group and a label; assign it in JobHud.prefab", this);
            enabled = false;
            return;
        }

        group.alpha = 0f;
    }

    private void OnEnable() => LootPickup.Taken += OnTaken;

    private void OnDisable() => LootPickup.Taken -= OnTaken;

    private void OnTaken(LootPickup pickup)
    {
        label.text = $"+ {pickup.Item.DisplayName.ToUpperInvariant()}";

        show.Stop();
        show = Sequence.Create()
            .Chain(Tween.Alpha(group, 1f, fadeDuration, Ease.OutQuad))
            .ChainDelay(holdSeconds)
            .Chain(Tween.Alpha(group, 0f, fadeDuration, Ease.InQuad));
    }
}
