using PrimeTween;
using UnityEngine;

// A story item's copy at the map table's objective position (OP): hidden until its target is selected,
// then it pops in with a quick shake to draw the eye, and shrinks away on deselect. Scales rather than
// fades, so it works with the props' opaque materials. Must start active in the scene: Awake records the
// authored scale and hides it
public class StoryItemDisplay : MonoBehaviour
{
    [SerializeField, Tooltip("Seconds to pop in")]
    private float popInDuration = 0.25f;

    [SerializeField, Tooltip("Seconds to pop out/shrink away")]
    private float popOutDuration = 0.15f;

    [SerializeField, Tooltip("Degrees the shake swings around the local Z axis")]
    private float shakeStrength = 8f;

    [SerializeField, Tooltip("Seconds the shake lasts")]
    private float shakeDuration = 0.4f;

    private Vector3 shownScale;
    private Sequence anim;

    private void Awake()
    {
        shownScale = transform.localScale;
        gameObject.SetActive(false);
    }

    [ContextMenu("Show")]
    public void Show()
    {
        anim.Stop();
        gameObject.SetActive(true);
        transform.localScale = Vector3.zero;
        anim = Sequence.Create()
            .Chain(Tween.Scale(transform, shownScale, popInDuration, Ease.OutBack))
            .Chain(Tween.ShakeLocalRotation(transform, new Vector3(0f, 0f, shakeStrength), shakeDuration));
    }

    [ContextMenu("Hide")]
    public void Hide()
    {
        if (!gameObject.activeSelf) return;
        anim.Stop();
        anim = Sequence.Create()
            .Chain(Tween.Scale(transform, Vector3.zero, popOutDuration, Ease.InBack))
            .ChainCallback(this, self => self.gameObject.SetActive(false));
    }
}
