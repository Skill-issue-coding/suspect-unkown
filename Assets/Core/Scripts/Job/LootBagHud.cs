using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

// The bag icon in the bottom-right corner: an outline at the start, filling from the bottom as required
// loot is taken (collected over required). Reacts to JobObjectives.Changed only; the fill eases on
// display, while the count itself stays exact
public class LootBagHud : MonoBehaviour
{
    [SerializeField, Tooltip("The level's objectives. The HUD shows its Collected over Required")]
    private JobObjectives objectives;

    [SerializeField, Tooltip("The solid bag over the outline. Image Type Filled, Vertical, origin Bottom")]
    private Image fill;

    [SerializeField, Tooltip("Seconds to ease the fill toward a new value")]
    private float easeDuration = 0.35f;

    private Tween ease;
    private float Target => objectives.Required == 0 ? 1f : (float)objectives.Collected / objectives.Required;

    private void Awake()
    {
        if (objectives == null || fill == null)
        {
            Debug.LogWarning($"{name} needs objectives and a fill image", this);
            enabled = false;
        }
    }

    private void OnEnable() => objectives.Changed += OnChanged;

    private void OnDisable() => objectives.Changed -= OnChanged;

    private void Start() => fill.fillAmount = Target;

    private void OnChanged()
    {
        ease.Stop();
        ease = Tween.UIFillAmount(fill, Target, easeDuration, Ease.OutCubic);
    }

}
