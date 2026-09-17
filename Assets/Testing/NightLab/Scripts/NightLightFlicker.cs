using UnityEngine;

/// <summary>
/// Flickers a NightLabLight for night ambience: a buzzing sodium lamp,
/// a dying fluorescent tube, or a pulsing alarm light.
/// </summary>
[RequireComponent(typeof(NightLabLight))]
public class NightLightFlicker : MonoBehaviour
{
    public enum FlickerMode
    {
        Buzz,
        Dropout,
        Pulse
    }

    [SerializeField] private FlickerMode m_Mode = FlickerMode.Dropout;

    [Tooltip("Buzz: noise speed. Dropout: average dropouts per second. Pulse: pulses per second.")]
    [SerializeField] private float m_Speed = 0.6f;

    [Range(0f, 1f)]
    [SerializeField] private float m_Depth = 0.85f;

    private NightLabLight m_NightLight;
    private float m_Seed;
    private float m_DropoutTimer;
    private float m_DropoutLength;

    public void Configure(FlickerMode mode, float speed, float depth)
    {
        m_Mode = mode;
        m_Speed = speed;
        m_Depth = depth;
    }

    private void Awake()
    {
        m_NightLight = GetComponent<NightLabLight>();
        m_Seed = Random.value * 100f;
    }

    private void Update()
    {
        float brightness = m_Mode switch
        {
            FlickerMode.Buzz => 1f - m_Depth * Mathf.PerlinNoise1D(Time.time * m_Speed * 10f + m_Seed),
            FlickerMode.Dropout => EvaluateDropout(),
            FlickerMode.Pulse => 1f - m_Depth * (0.5f + 0.5f * Mathf.Cos(Time.time * m_Speed * Mathf.PI * 2f)),
            _ => 1f
        };

        m_NightLight.Refresh(brightness);
    }

    private void OnDisable()
    {
        if (m_NightLight != null)
            m_NightLight.Refresh(1f);
    }

    private float EvaluateDropout()
    {
        if (m_DropoutLength > 0f)
        {
            m_DropoutLength -= Time.deltaTime;
            // Stutter while off instead of a clean cut.
            return Random.value > 0.5f ? 1f - m_Depth : 1f - m_Depth * 0.4f;
        }

        m_DropoutTimer -= Time.deltaTime;
        if (m_DropoutTimer <= 0f)
        {
            m_DropoutTimer = Random.Range(0.2f, 2f) / Mathf.Max(0.01f, m_Speed);
            m_DropoutLength = Random.Range(0.03f, 0.25f);
        }

        return 1f;
    }
}
