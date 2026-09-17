using UnityEngine;

/// <summary>
/// Tags a light so night presets can recolor it, and drives the emission of its
/// glow mesh (lamp head, tube, sign) so bloom picks it up.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Light))]
public class NightLabLight : MonoBehaviour
{
    public enum LightRole
    {
        Street,
        Interior,
        Accent
    }

    [SerializeField] private LightRole m_Role;
    [SerializeField] private Renderer m_GlowRenderer;
    [SerializeField] private float m_GlowStrength = 6f;
    [SerializeField] private Color m_Color = Color.white;
    [SerializeField] private float m_Intensity = 1f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private MaterialPropertyBlock m_PropertyBlock;
    private Light m_Light;

    public LightRole Role => m_Role;
    public float Intensity => m_Intensity;

    public void Setup(LightRole role, Renderer glowRenderer, float glowStrength)
    {
        m_Role = role;
        m_GlowRenderer = glowRenderer;
        m_GlowStrength = glowStrength;
    }

    public void SetColor(Color color, float intensity)
    {
        m_Color = color;
        m_Intensity = intensity;
        Refresh(1f);
    }

    /// <summary>Scales light and glow together; used by NightLightFlicker.</summary>
    public void Refresh(float brightness)
    {
        if (m_Light == null)
            m_Light = GetComponent<Light>();

        m_Light.color = m_Color;
        m_Light.intensity = m_Intensity * brightness;

        if (m_GlowRenderer == null)
            return;

        m_PropertyBlock ??= new MaterialPropertyBlock();
        m_GlowRenderer.GetPropertyBlock(m_PropertyBlock);
        m_PropertyBlock.SetColor(BaseColorId, m_Color * 0.2f);
        m_PropertyBlock.SetColor(EmissionColorId, m_Color * (m_GlowStrength * brightness));
        m_GlowRenderer.SetPropertyBlock(m_PropertyBlock);
    }

    private void OnEnable()
    {
        Refresh(1f);
    }

    private void OnValidate()
    {
        Refresh(1f);
    }
}
