using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The menu button look: a dark translucent plate with a thin light border, an icon and a
// title. Hover brightens the border, grows an amber accent bar on the left edge and nudges
// the content right. "Selected" holds the hover look, for tabs and the chosen category.
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

    private Button button;
    private Vector2 contentRest;
    private bool hovered, selected;
    private float value;
    private Coroutine routine;

    public Button Button => button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (content != null) contentRest = content.anchoredPosition;
        Apply(0f);
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

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; Animate(); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; Animate(); }
    public void OnSelect(BaseEventData eventData) { hovered = true; Animate(); }
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
