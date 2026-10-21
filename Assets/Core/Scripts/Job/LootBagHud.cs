using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

// The bag in the job HUD's carry panel: an outline when empty, filling from the bottom by slots used
// (count over capacity). Reacts to LootBag.Changed only; the fill eases on display, while the count
// itself stays exact. Lives on JobHud.prefab's root
public class LootBagHud : MonoBehaviour
{
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
    private int shownCount;

    private void Awake()
    {
        if (fill == null)
        {
            Debug.LogWarning($"{name} needs a fill image", this);
            enabled = false;
        }
    }

    private void OnEnable() => LootBag.Changed += OnChanged;

    private void OnDisable() => LootBag.Changed -= OnChanged;

    // After every OnEnable, so the level's LootBag is Active by now
    private void Start()
    {
        shownCount = Count;
        fill.fillAmount = Target;
    }

    private static int Count => LootBag.Active != null ? LootBag.Active.Count : 0;

    private static float Target => LootBag.Active != null ? (float)LootBag.Active.Count / LootBag.Active.Capacity : 0f;

    private void OnChanged()
    {
        ease.Stop();
        ease = Tween.UIFillAmount(fill, Target, easeDuration, Ease.OutCubic);

        // Only an item going in punches; emptying the bag at the van (L4) just drains it
        bool added = Count > shownCount;
        shownCount = Count;
        if (bag == null || !added) return;

        // Finish a running punch first, so two quick items cannot leave the bag stuck at a punched scale
        punch.Complete();
        punch = Tween.PunchScale(bag, Vector3.one * punchStrength, duration: 0.3f, frequency: 6);
    }
}
