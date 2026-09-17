using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One complete night look: skybox, ambient light, fog, moonlight, post-processing
/// and the colors of every NightLabLight in the scene. Edit the referenced sky
/// material and volume profile directly, then re-apply from the Night Lab window.
/// </summary>
[CreateAssetMenu(fileName = "NightPreset", menuName = "Suspect Unknown/Night Preset")]
public class NightPreset : ScriptableObject
{
    [Header("Sky & Post-processing")]
    public Material skybox;
    public VolumeProfile volumeProfile;

    [Header("Ambient")]
    public AmbientMode ambientMode = AmbientMode.Trilight;
    [ColorUsage(false, true)] public Color ambientSky = new Color(0.1f, 0.14f, 0.26f);
    [ColorUsage(false, true)] public Color ambientEquator = new Color(0.06f, 0.07f, 0.12f);
    [ColorUsage(false, true)] public Color ambientGround = new Color(0.02f, 0.02f, 0.03f);
    public float ambientIntensity = 1f;
    [Range(0f, 1f)] public float reflectionIntensity = 0.3f;

    [Header("Fog")]
    public bool fog = true;
    public Color fogColor = new Color(0.04f, 0.06f, 0.11f);
    [Range(0f, 0.1f)] public float fogDensity = 0.012f;

    [Header("Moonlight (main directional light)")]
    public Color moonColor = new Color(0.62f, 0.72f, 1f);
    public float moonIntensity = 0.35f;
    public Vector3 moonRotation = new Vector3(40f, -40f, 0f);
    [Range(0f, 1f)] public float moonShadowStrength = 0.85f;

    [Header("Scene lights (NightLabLight roles)")]
    public Color streetLightColor = new Color(1f, 0.72f, 0.42f);
    public float streetLightIntensity = 14f;
    public Color interiorLightColor = new Color(0.8f, 0.92f, 1f);
    public float interiorLightIntensity = 3f;
    public Color accentLightColor = new Color(0.2f, 1f, 0.45f);
    public float accentLightIntensity = 1.5f;

    private static readonly int MoonDirectionId = Shader.PropertyToID("_MoonDirection");

    public void Apply(Light sun, Volume volume)
    {
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientSkyColor = ambientSky;
        RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = reflectionIntensity;

        RenderSettings.fog = fog;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;

        if (sun != null)
        {
            sun.useColorTemperature = false;
            sun.color = moonColor;
            sun.intensity = moonIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = moonShadowStrength;
            sun.transform.rotation = Quaternion.Euler(moonRotation);
            RenderSettings.sun = sun;

            // Keep the painted moon where the shadows say it is.
            if (skybox != null && skybox.HasProperty(MoonDirectionId))
                skybox.SetVector(MoonDirectionId, -sun.transform.forward);
        }

        if (volume != null && volumeProfile != null)
            volume.sharedProfile = volumeProfile;

        foreach (NightLabLight nightLight in FindObjectsByType<NightLabLight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            switch (nightLight.Role)
            {
                case NightLabLight.LightRole.Street:
                    nightLight.SetColor(streetLightColor, streetLightIntensity);
                    break;
                case NightLabLight.LightRole.Interior:
                    nightLight.SetColor(interiorLightColor, interiorLightIntensity);
                    break;
                case NightLabLight.LightRole.Accent:
                    nightLight.SetColor(accentLightColor, accentLightIntensity);
                    break;
            }
        }

        DynamicGI.UpdateEnvironment();
    }
}
