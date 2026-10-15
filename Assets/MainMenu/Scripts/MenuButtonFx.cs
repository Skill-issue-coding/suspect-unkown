using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The menu button look: a dark translucent plate with a thin light border, an icon and a
// title. Hover brightens the border, grows an amber accent bar on the left edge and nudges
// the content right. "Selected" holds the hover look, for tabs and the chosen category.
// "Locked" makes the button unclickable, drops the hover, dims the content and swaps the
// icon for a lock, for content the save has not unlocked yet.
// Animates only while a transition is running, never every frame.
[RequireComponent(typeof(Button))]
public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Tooltip("Background plate")]
    [SerializeField] private Image fill;
    [Tooltip("Thin border drawn over the plate")]
    [SerializeField] private Image border;
    [Tooltip("Accent bar on the left edge. Its width animates")]
    [SerializeField] private RectTransform accent;
    [Tooltip("Icon and labels. Slides right on hover")]
    [SerializeField] private RectTransform content;

    [Tooltip("Plate color at rest and hovered")]
    [SerializeField] private Color fillIdle = new Color(0.07f, 0.08f, 0.09f, 0.72f), fillHover = new Color(0.12f, 0.13f, 0.14f, 0.85f);
    [Tooltip("Border color at rest and hovered")]
    [SerializeField] private Color borderIdle = new Color(1f, 1f, 1f, 0.28f), borderHover = new Color(1f, 1f, 1f, 0.95f);
    [Tooltip("Accent bar width when hovered, pixels")]
    [SerializeField] private float accentWidth = 6f;
    [Tooltip("How far the content slides when hovered, pixels")]
    [SerializeField] private float slide = 10f;
    [Tooltip("Seconds for the hover transition")]
    [SerializeField] private float duration = 0.12f;

    [Header("Locked")]
    [Tooltip("Icon swapped for the lock while locked. Empty: the icon stays")]
    [SerializeField] private Image icon;
    [Tooltip("Sprite the icon shows while locked")]
    [SerializeField] private Sprite lockedIcon;
    [Tooltip("Opacity of the icon and labels while locked, 0 to 1")]
    [Range(0f, 1f)]
    [SerializeField] private float lockedAlpha = 0.4f;

    private Button button;
    private Vector2 contentRest;
    private bool hovered, selected, locked;
    private float value;
    private Coroutine routine;
    private Sprite openIcon;
    private Graphic[] contentGraphics;
    private Color[] contentColors;

    public Button Button => button;

    private void Awake() => Init();

    // Also run from SetLocked: another script may lock this button before its own Awake has run
    // (Awake waits until the object is first active). Guarded so the authored colors are cached once
    private void Init()
    {
        if (button != null) return;
        button = GetComponent<Button>();
        if (content != null)
        {
            contentRest = content.anchoredPosition;
            // Cached once with their authored colors, so locking only ever scales alpha from those
            contentGraphics = content.GetComponentsInChildren<Graphic>(true);
            contentColors = new Color[contentGraphics.Length];
            for (int i = 0; i < contentGraphics.Length; i++) contentColors[i] = contentGraphics[i].color;
        }
        if (icon != null) openIcon = icon.sprite;
        Apply(0f);
    }

    // Locked buttons cannot be clicked or hovered, and show the lock in place of their icon
    public void SetLocked(bool on)
    {
        Init();
        locked = on;
        button.interactable = !on;
        if (on) hovered = false;

        if (icon != null && lockedIcon != null) icon.sprite = on ? lockedIcon : openIcon;
        if (contentGraphics != null)
            for (int i = 0; i < contentGraphics.Length; i++)
            {
                Color color = contentColors[i];
                if (on) color.a *= lockedAlpha;
                contentGraphics[i].color = color;
            }
        Animate();
    }

    private void OnDisable()
    {
        hovered = false;
        routine = null;
        Apply(selected ? 1f : 0f);
    }

    public void SetSelected(bool on)
    {
        selected = on;
        Animate();
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = !locked; Animate(); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; Animate(); }
    public void OnSelect(BaseEventData eventData) { hovered = !locked; Animate(); }
    public void OnDeselect(BaseEventData eventData) { hovered = false; Animate(); }

    private void Animate()
    {
        if (!isActiveAndEnabled) { Apply(hovered || selected ? 1f : 0f); return; }
        if (routine == null) routine = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        float target;
        do
        {
            target = hovered || selected ? 1f : 0f;
            Apply(Mathf.MoveTowards(value, target, Time.unscaledDeltaTime / duration));
            yield return null;
        } while (!Mathf.Approximately(value, hovered || selected ? 1f : 0f));
        routine = null;
    }

    private void Apply(float t)
    {
        value = t;
        float e = 1f - (1f - t) * (1f - t);
        if (fill != null) fill.color = Color.Lerp(fillIdle, fillHover, e);
        if (border != null) border.color = Color.Lerp(borderIdle, borderHover, e);
        if (accent != null) accent.sizeDelta = new Vector2(accentWidth * e, accent.sizeDelta.y);
        if (content != null) content.anchoredPosition = contentRest + new Vector2(slide * e, 0f);
    }
}
