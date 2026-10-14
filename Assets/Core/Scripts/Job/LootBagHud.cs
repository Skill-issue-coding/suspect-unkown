using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

// The bag in the job HUD's carry panel: an outline at the start, filling from the bottom as required
// loot is taken (collected over required). Reacts to JobObjectives.Changed only; the fill eases on
// display, while the count itself stays exact. Lives on JobHud.prefab's root
public class LootBagHud : MonoBehaviour
{
    [SerializeField, Tooltip("The level's objectives. The HUD shows its Collected over Required")]
    private JobObjectives objectives;

    [SerializeField, Tooltip("The solid bag over the outline. Image Type Filled, Vertical, origin Bottom")]
    private Image fill;

    [SerializeField, Tooltip("Optional. Punched in scale when an item goes in, so the change catches the eye")]
    private RectTransform bag;

    [SerializeField, Tooltip("Seconds to ease the fill toward a new value")]
    private float easeDuration = 0.35f;

    [SerializeField, Tooltip("Scale added at the peak of the punch, as a fraction of the bag's size")]
    private float punchStrength = 0.15f;

    private Tween ease;
    private Tween punch;
    private float Target => objectives.Required == 0 ? 1f : (float)objectives.Collected / objectives.Required;

    private void Awake()
    {
        if (objectives == null || fill == null)
        {
            Debug.LogWarning($"{name} needs objectives and a fill image", this);
            enabled = false;
        }
    }

    // Guarded: disabling in Awake still calls OnDisable, with objectives missing
    private void OnEnable()
    {
        if (objectives != null) objectives.Changed += OnChanged;
    }

    private void OnDisable()
    {
        if (objectives != null) objectives.Changed -= OnChanged;
    }

    private void Start() => fill.fillAmount = Target;

    private void OnChanged()
    {
        ease.Stop();
        ease = Tween.UIFillAmount(fill, Target, easeDuration, Ease.OutCubic);

        if (bag == null) return;
        // Finish a running punch first, so two quick items cannot leave the bag stuck at a punched scale
        punch.Complete();
        punch = Tween.PunchScale(bag, Vector3.one * punchStrength, duration: 0.3f, frequency: 6);
    }
}
