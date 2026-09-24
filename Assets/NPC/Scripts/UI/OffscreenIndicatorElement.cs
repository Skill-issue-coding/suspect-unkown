using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Draws one edge-of-screen suspicion indicator. Decides nothing: NpcSuspicionIndicator
// creates it through OffscreenIndicatorHUD, then sets its state and places it every frame.
[RequireComponent(typeof(CanvasGroup))]
public class OffscreenIndicatorElement : MonoBehaviour
{
    [SerializeField] private RectTransform arrowPivot;
    [SerializeField] private Image arrow;
    [SerializeField] private TMP_Text icon;

    private CanvasGroup group;
    private RectTransform rect;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        rect = GetComponent<RectTransform>();
        group.alpha = 0f;
    }

    public void SetState(string text, Color color)
    {
        icon.text = text;
        icon.color = color;
        arrow.color = color;
    }

    // angle is in maths convention: 0 points right, counter-clockwise positive. The arc sprite's tip
    // points up (90), hence the offset. On an Overlay canvas, position is in screen pixels.
    public void Place(Vector2 screenPosition, float angle, float fill, float alpha)
    {
        rect.position = screenPosition;
        arrowPivot.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        arrow.fillAmount = fill;
        group.alpha = alpha;
    }
}
