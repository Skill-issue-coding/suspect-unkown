using PrimeTween;
using UnityEngine;

// Hides a building's roof and upper floor while the player is inside, so the top-down camera can see
// in. The roof dithers away through the OccluderWall shader's _Hide input, set per renderer with a
// property block, so the shared material is untouched and the roof keeps casting its shadow (the
// interior stays as dark as it was baked). Its triggers sit on the Volume layer, which collides with the
// player only, so NPCs walking inside change nothing. No link to the door: the player can only get in
// once a door is open
public class InteriorReveal : MonoBehaviour
{
    [SerializeField, Tooltip("Everything under this fades while the player is inside. Its renderers need the OccluderWall shader")]
    private Transform hiddenRoot;

    [SerializeField, Tooltip("Seconds to fade out or back in")]
    private float fadeDuration = 0.25f;

    private static readonly int HideId = Shader.PropertyToID("_Hide");

    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private Tween fade;
    private float current;

    // Counted, not a bool: with several trigger boxes, walking between rooms sends Enter on the next box
    // before Exit on the last
    private int inside;

    private void Awake()
    {
        if (hiddenRoot == null)
        {
            Debug.LogWarning($"{name} has no hidden root; nothing will fade", this);
            enabled = false;
            return;
        }

        // Cached once: pieces added under the root at runtime are not picked up
        renderers = hiddenRoot.GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
    }

    // Trigger events reach this through the player's CharacterController (see PlayerConduct)
    private void OnTriggerEnter(Collider other)
    {
        if (!enabled || !other.TryGetComponent(out CharacterController _)) return;
        if (++inside == 1) FadeTo(1f);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!enabled || inside == 0 || !other.TryGetComponent(out CharacterController _)) return;
        if (--inside == 0) FadeTo(0f);
    }

    // Starts from the current value, so stepping in and straight back out reverses smoothly
    private void FadeTo(float hide)
    {
        fade.Stop();
        fade = Tween.Custom(this, current, hide, fadeDuration, (self, value) => self.Apply(value), Ease.OutQuad);
    }

    private void Apply(float hide)
    {
        current = hide;
        block.SetFloat(HideId, hide);
        foreach (Renderer r in renderers)
            r.SetPropertyBlock(block);
    }
}
