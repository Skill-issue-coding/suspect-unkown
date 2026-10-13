using PrimeTween;
using UnityEngine;

// Shows or hides the highlight rim around an interactable. Holds renderers that draw only the
// outline material; the player side decides when. Starts hidden
public class InteractOutline : MonoBehaviour
{
    private static readonly int FadeId = Shader.PropertyToID("_Fade");

    [SerializeField, Tooltip("Seconds to fade in or out")]
    private float fadeDuration = 0.12f;

    [SerializeField, Tooltip("Renderers that draw only the outline material")]
    private Renderer[] outlines;

    private MaterialPropertyBlock block;
    private Tween fade;
    private float current;

    private void Awake()
    {
        foreach (Renderer outline in outlines)
            outline.enabled = false;

        block = new MaterialPropertyBlock();
        Apply(0f);
        SetRenderers(false);
    }

    public void SetVisible(bool visible)
    {
        fade.Stop();
        if (visible) SetRenderers(true);

        fade = Tween.Custom(this, current, visible ? 1f : 0f, fadeDuration,
            (self, value) => self.Apply(value), Ease.OutQuad);

        if (!visible)
            fade.OnComplete(this, self => self.SetRenderers(false), warnIfTargetDestroyed: false);
    }

    private void Apply(float value)
    {
        current = value;
        block.SetFloat(FadeId, value);
        foreach (Renderer outline in outlines)
            outline.SetPropertyBlock(block);
    }

    private void SetRenderers(bool enabled)
    {
        foreach (Renderer outline in outlines)
            outline.enabled = enabled;
    }

    [ContextMenu("Show")]
    private void Show() => SetVisible(true);

    [ContextMenu("Hide")]
    private void Hide() => SetVisible(false);
}
