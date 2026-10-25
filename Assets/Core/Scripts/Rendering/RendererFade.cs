using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Fades one renderer in and out for BuildingFloors and FloorHideable (decision 26). Submeshes on the
// OccluderWall shader dither away through _Hide; transparent URP Lit submeshes (glass) fade their base
// color's alpha instead, since a transparent material cannot dither. Any other shader (outlines,
// text, the vision cone) is left alone. Values go through per-submesh property blocks that are read
// back first, so anything else a script keeps in the same block survives. Built once per renderer
public class RendererFade
{
    private const string LitShader = "Universal Render Pipeline/Lit";

    private static readonly int HideId = Shader.PropertyToID("_Hide");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // Shared: every fade runs on the main thread and fills the block right before setting it
    private static readonly MaterialPropertyBlock Block = new();

    private readonly Renderer renderer;
    private readonly bool keepShadowWhenHidden;
    private readonly ShadowCastingMode originalShadow;
    private readonly int[] ditherSlots;
    private readonly int[] alphaSlots;
    private readonly Color[] alphaColors;
    private float current;

    public bool CanFade => ditherSlots.Length > 0 || alphaSlots.Length > 0;

    // keepShadowWhenHidden: fully hidden, the renderer still casts its whole shadow. On for the
    // building, so the floor below stays as dark as it was lit; off for NPCs and props
    public RendererFade(Renderer renderer, bool keepShadowWhenHidden)
    {
        this.renderer = renderer;
        this.keepShadowWhenHidden = keepShadowWhenHidden;
        originalShadow = renderer.shadowCastingMode;

        var dither = new List<int>();
        var alpha = new List<int>();
        var colors = new List<Color>();
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material m = materials[i];
            if (m == null) continue;

            if (m.HasProperty(HideId))
            {
                dither.Add(i);
            }
            else if (m.renderQueue >= (int)RenderQueue.Transparent && m.shader.name == LitShader && m.HasProperty(BaseColorId))
            {
                alpha.Add(i);
                colors.Add(m.GetColor(BaseColorId));
            }
        }

        ditherSlots = dither.ToArray();
        alphaSlots = alpha.ToArray();
        alphaColors = colors.ToArray();
    }

    // 0 shown, 1 gone
    public void Set(float hide)
    {
        hide = Mathf.Clamp01(hide);
        if (Mathf.Approximately(hide, current)) return;
        current = hide;

        // The dither also runs in the shadow pass, so a fully dithered renderer casts no shadow. To keep
        // it, draw shadows only and lift the dither instead. Without a shadow to keep, a hidden renderer
        // is not drawn at all: glass at zero alpha still shows its specular highlights
        bool gone = hide >= 1f;
        bool shadowsOnly = keepShadowWhenHidden && gone;
        renderer.shadowCastingMode = shadowsOnly ? ShadowCastingMode.ShadowsOnly : originalShadow;
        renderer.forceRenderingOff = gone && !keepShadowWhenHidden;
        float dither = shadowsOnly ? 0f : hide;

        foreach (int slot in ditherSlots)
        {
            renderer.GetPropertyBlock(Block, slot);
            Block.SetFloat(HideId, dither);
            renderer.SetPropertyBlock(Block, slot);
        }

        for (int i = 0; i < alphaSlots.Length; i++)
        {
            Color color = alphaColors[i];
            color.a *= 1f - hide;
            renderer.GetPropertyBlock(Block, alphaSlots[i]);
            Block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(Block, alphaSlots[i]);
        }
    }
}
