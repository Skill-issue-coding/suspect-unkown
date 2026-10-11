using System;
using System.Collections;
using UnityEngine;

// Blends the menu scene between its daytime look (the safehouse, where money is spent)
// and its night look (the city map, where the next job is chosen). One value drives
// everything: 0 is day, 1 is night. The skybox swaps at the halfway point.
public class DayNightBlend : MonoBehaviour
{
    [Serializable]
    public struct Look
    {
        [Tooltip("Sun or moon colour")]
        public Color lightColor;
        [Tooltip("Sun or moon intensity")]
        public float lightIntensity;
        [Tooltip("Sun or moon rotation, Euler degrees")]
        public Vector3 lightEuler;
        [Tooltip("Trilight ambient: sky, equator, ground")]
        public Color ambientSky, ambientEquator, ambientGround;
        [Tooltip("Skybox material for this look")]
        public Material skybox;
    }

    [Tooltip("The directional light playing both sun and moon")]
    [SerializeField] private Light sun;
    [Tooltip("Daytime look")]
    [SerializeField] private Look day;
    [Tooltip("Night-time look")]
    [SerializeField] private Look night;
    [Tooltip("Lights that only glow at night (street lamps, desk lamp). Their scene intensity is the night intensity")]
    [SerializeField] private Light[] nightLights;
    [Tooltip("Seconds for a full day to night blend")]
    [SerializeField] private float duration = 1.6f;

    private float[] nightIntensities;
    private float value;
    private Coroutine routine;

    private void Awake()
    {
        if (sun == null)
        {
            Debug.LogWarning($"{name} has no sun assigned", this);
            enabled = false;
            return;
        }

        nightIntensities = new float[nightLights.Length];
        for (int i = 0; i < nightLights.Length; i++) nightIntensities[i] = nightLights[i].intensity;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
    }

    public void Snap(float target)
    {
        if (!enabled) return;
        if (routine != null) StopCoroutine(routine);
        Apply(target);
    }

    public void BlendTo(float target)
    {
        if (!enabled) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Blend(target));
    }

    private IEnumerator Blend(float target)
    {
        while (!Mathf.Approximately(value, target))
        {
            Apply(Mathf.MoveTowards(value, target, Time.unscaledDeltaTime / duration));
            yield return null;
        }
        routine = null;
    }

    private void Apply(float t)
    {
        value = t;
        float s = Mathf.SmoothStep(0f, 1f, t);

        sun.color = Color.Lerp(day.lightColor, night.lightColor, s);
        sun.intensity = Mathf.Lerp(day.lightIntensity, night.lightIntensity, s);
        sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(day.lightEuler), Quaternion.Euler(night.lightEuler), s);

        RenderSettings.ambientSkyColor = Color.Lerp(day.ambientSky, night.ambientSky, s);
        RenderSettings.ambientEquatorColor = Color.Lerp(day.ambientEquator, night.ambientEquator, s);
        RenderSettings.ambientGroundColor = Color.Lerp(day.ambientGround, night.ambientGround, s);

        Material sky = s < 0.5f ? day.skybox : night.skybox;
        if (sky != null && RenderSettings.skybox != sky) RenderSettings.skybox = sky;

        for (int i = 0; i < nightLights.Length; i++)
        {
            nightLights[i].intensity = nightIntensities[i] * s;
            nightLights[i].enabled = s > 0.01f;
        }
    }
}
