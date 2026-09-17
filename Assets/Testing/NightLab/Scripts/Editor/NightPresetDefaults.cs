using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Starting points for the night looks. "Create / Reset Default Presets" writes these
/// to disk; after that, tweak the generated assets instead of this file.
/// </summary>
public static class NightPresetDefaults
{
    public const string RootFolder = "Assets/Testing/NightLab";
    public const string PresetFolder = RootFolder + "/Presets";
    const string SkyShaderName = "SuspectUnknown/Skybox/Night";

    class Definition
    {
        public string name;
        public Action<Material> sky;
        public Action<NightPreset> lighting;
        public Action<VolumeProfile> post;
    }

    static Color C(float r, float g, float b) => new Color(r, g, b);

    static readonly Definition[] Definitions =
    {
        new Definition
        {
            name = "1_MoonlitBlue",
            sky = m =>
            {
                m.SetColor("_TopColor", C(0.01f, 0.02f, 0.06f));
                m.SetColor("_HorizonColor", C(0.07f, 0.1f, 0.2f));
                m.SetColor("_BottomColor", C(0.02f, 0.02f, 0.03f));
                m.SetColor("_HorizonGlowColor", C(0.25f, 0.15f, 0.1f));
                m.SetFloat("_HorizonGlowIntensity", 0.3f);
                m.SetFloat("_HorizonGlowFalloff", 10f);
                m.SetFloat("_StarAmount", 0.3f);
                m.SetFloat("_StarBrightness", 4f);
                m.SetFloat("_BandIntensity", 0.3f);
                m.SetFloat("_MoonIntensity", 3.5f);
                m.SetFloat("_MoonSize", 3f);
                m.SetColor("_MoonGlowColor", C(0.35f, 0.45f, 0.7f));
                m.SetFloat("_MoonGlowIntensity", 0.7f);
                m.SetFloat("_MoonGlowExponent", 48f);
                m.SetColor("_CloudColor", C(0.06f, 0.08f, 0.13f));
                m.SetFloat("_CloudCoverage", 0.25f);
            },
            lighting = p =>
            {
                p.ambientSky = C(0.1f, 0.14f, 0.26f);
                p.ambientEquator = C(0.06f, 0.07f, 0.12f);
                p.ambientGround = C(0.02f, 0.02f, 0.03f);
                p.fogColor = C(0.04f, 0.06f, 0.11f);
                p.fogDensity = 0.012f;
                p.moonColor = C(0.62f, 0.72f, 1f);
                p.moonIntensity = 0.35f;
                p.moonRotation = new Vector3(40f, -40f, 0f);
                p.moonShadowStrength = 0.85f;
                p.streetLightColor = C(1f, 0.72f, 0.42f);
                p.streetLightIntensity = 14f;
                p.interiorLightColor = C(0.8f, 0.92f, 1f);
                p.interiorLightIntensity = 3f;
                p.accentLightColor = C(0.2f, 1f, 0.45f);
            },
            post = v =>
            {
                AddBloom(v, 0.9f, 1f, 0.7f, Color.white);
                AddTonemapping(v, TonemappingMode.ACES);
                AddColorAdjustments(v, 0f, 12f, -10f, C(0.95f, 0.97f, 1f));
                AddWhiteBalance(v, -15f, 0f);
                AddSplitToning(v, C(0.35f, 0.45f, 0.75f), C(1f, 0.8f, 0.6f), -20f);
                AddVignette(v, 0.35f, 0.4f);
                AddFilmGrain(v, FilmGrainLookup.Thin1, 0.15f);
                AddChromaticAberration(v, 0.05f);
            }
        },
        new Definition
        {
            name = "2_SodiumSmog",
            sky = m =>
            {
                m.SetColor("_TopColor", C(0.03f, 0.025f, 0.03f));
                m.SetColor("_HorizonColor", C(0.12f, 0.08f, 0.06f));
                m.SetColor("_BottomColor", C(0.03f, 0.02f, 0.02f));
                m.SetColor("_HorizonGlowColor", C(0.6f, 0.3f, 0.1f));
                m.SetFloat("_HorizonGlowIntensity", 1.2f);
                m.SetFloat("_HorizonGlowFalloff", 5f);
                m.SetFloat("_StarAmount", 0f);
                m.SetFloat("_BandIntensity", 0f);
                m.SetFloat("_MoonIntensity", 1f);
                m.SetColor("_MoonGlowColor", C(0.4f, 0.3f, 0.2f));
                m.SetFloat("_MoonGlowIntensity", 0.2f);
                m.SetColor("_CloudColor", C(0.1f, 0.07f, 0.05f));
                m.SetFloat("_CloudCoverage", 0.85f);
                m.SetFloat("_CloudOpacity", 0.9f);
            },
            lighting = p =>
            {
                p.ambientSky = C(0.2f, 0.14f, 0.1f);
                p.ambientEquator = C(0.12f, 0.08f, 0.06f);
                p.ambientGround = C(0.03f, 0.02f, 0.02f);
                p.fogColor = C(0.12f, 0.08f, 0.06f);
                p.fogDensity = 0.02f;
                p.moonColor = C(0.7f, 0.65f, 0.6f);
                p.moonIntensity = 0.15f;
                p.moonRotation = new Vector3(60f, -20f, 0f);
                p.moonShadowStrength = 0.6f;
                p.streetLightColor = C(1f, 0.55f, 0.2f);
                p.streetLightIntensity = 18f;
                p.interiorLightColor = C(0.85f, 1f, 0.85f);
                p.interiorLightIntensity = 3f;
                p.accentLightColor = C(0.2f, 1f, 0.45f);
            },
            post = v =>
            {
                AddBloom(v, 0.8f, 1.4f, 0.75f, C(1f, 0.85f, 0.7f));
                AddTonemapping(v, TonemappingMode.ACES);
                AddColorAdjustments(v, 0f, 18f, -20f, C(1f, 0.95f, 0.88f));
                AddWhiteBalance(v, 10f, 5f);
                AddSplitToning(v, C(0.3f, 0.4f, 0.35f), C(1f, 0.7f, 0.4f), 0f);
                AddVignette(v, 0.4f, 0.4f);
                AddFilmGrain(v, FilmGrainLookup.Medium3, 0.3f);
                AddChromaticAberration(v, 0.1f);
            }
        },
        new Definition
        {
            name = "3_Noir",
            sky = m =>
            {
                m.SetColor("_TopColor", C(0.004f, 0.004f, 0.006f));
                m.SetColor("_HorizonColor", C(0.02f, 0.022f, 0.028f));
                m.SetColor("_BottomColor", C(0.005f, 0.005f, 0.005f));
                m.SetColor("_HorizonGlowColor", C(0.05f, 0.05f, 0.06f));
                m.SetFloat("_HorizonGlowIntensity", 0.2f);
                m.SetFloat("_StarAmount", 0.12f);
                m.SetFloat("_StarBrightness", 2f);
                m.SetFloat("_BandIntensity", 0f);
                m.SetFloat("_MoonIntensity", 5f);
                m.SetFloat("_MoonSize", 2.2f);
                m.SetColor("_MoonGlowColor", C(0.2f, 0.2f, 0.22f));
                m.SetFloat("_MoonGlowIntensity", 0.5f);
                m.SetFloat("_MoonGlowExponent", 128f);
                m.SetColor("_CloudColor", C(0.02f, 0.02f, 0.025f));
                m.SetFloat("_CloudCoverage", 0.4f);
            },
            lighting = p =>
            {
                p.ambientSky = C(0.05f, 0.055f, 0.07f);
                p.ambientEquator = C(0.03f, 0.03f, 0.04f);
                p.ambientGround = C(0.01f, 0.01f, 0.01f);
                p.fogColor = C(0.015f, 0.015f, 0.02f);
                p.fogDensity = 0.02f;
                p.moonColor = C(0.85f, 0.9f, 1f);
                p.moonIntensity = 0.2f;
                p.moonRotation = new Vector3(35f, -60f, 0f);
                p.moonShadowStrength = 1f;
                p.streetLightColor = C(1f, 0.95f, 0.85f);
                p.streetLightIntensity = 16f;
                p.interiorLightColor = Color.white;
                p.interiorLightIntensity = 2.5f;
                p.accentLightColor = C(1f, 0.15f, 0.1f);
            },
            post = v =>
            {
                AddBloom(v, 1f, 0.7f, 0.6f, Color.white);
                AddTonemapping(v, TonemappingMode.ACES);
                AddColorAdjustments(v, -0.2f, 30f, -65f, Color.white);
                AddVignette(v, 0.5f, 0.35f);
                AddFilmGrain(v, FilmGrainLookup.Medium4, 0.4f);
            }
        },
        new Definition
        {
            name = "4_NeonTeal",
            sky = m =>
            {
                m.SetColor("_TopColor", C(0.01f, 0.005f, 0.03f));
                m.SetColor("_HorizonColor", C(0.08f, 0.03f, 0.12f));
                m.SetColor("_BottomColor", C(0.02f, 0.01f, 0.03f));
                m.SetColor("_HorizonGlowColor", C(0.4f, 0.1f, 0.35f));
                m.SetFloat("_HorizonGlowIntensity", 0.8f);
                m.SetFloat("_HorizonGlowFalloff", 7f);
                m.SetFloat("_StarAmount", 0.2f);
                m.SetFloat("_BandIntensity", 0f);
                m.SetFloat("_MoonIntensity", 3f);
                m.SetColor("_MoonGlowColor", C(0.2f, 0.6f, 0.7f));
                m.SetFloat("_MoonGlowIntensity", 0.5f);
                m.SetColor("_CloudColor", C(0.06f, 0.03f, 0.08f));
                m.SetFloat("_CloudCoverage", 0.5f);
            },
            lighting = p =>
            {
                p.ambientSky = C(0.12f, 0.08f, 0.22f);
                p.ambientEquator = C(0.05f, 0.1f, 0.12f);
                p.ambientGround = C(0.02f, 0.01f, 0.03f);
                p.fogColor = C(0.06f, 0.03f, 0.09f);
                p.fogDensity = 0.018f;
                p.moonColor = C(0.45f, 0.85f, 0.95f);
                p.moonIntensity = 0.3f;
                p.moonRotation = new Vector3(45f, 120f, 0f);
                p.moonShadowStrength = 0.8f;
                p.streetLightColor = C(1f, 0.3f, 0.7f);
                p.streetLightIntensity = 14f;
                p.interiorLightColor = C(0.3f, 0.95f, 1f);
                p.interiorLightIntensity = 3.5f;
                p.accentLightColor = C(1f, 0.85f, 0.2f);
            },
            post = v =>
            {
                AddBloom(v, 0.7f, 1.6f, 0.8f, C(0.9f, 0.9f, 1f));
                AddTonemapping(v, TonemappingMode.ACES);
                AddColorAdjustments(v, 0f, 20f, 10f, Color.white);
                AddWhiteBalance(v, -25f, 15f);
                AddSplitToning(v, C(0.2f, 0.8f, 0.85f), C(1f, 0.4f, 0.8f), 10f);
                AddVignette(v, 0.4f, 0.4f);
                AddFilmGrain(v, FilmGrainLookup.Thin2, 0.15f);
                AddChromaticAberration(v, 0.2f);
            }
        },
        new Definition
        {
            name = "5_StarrySuburb",
            sky = m =>
            {
                m.SetColor("_TopColor", C(0.005f, 0.012f, 0.035f));
                m.SetColor("_HorizonColor", C(0.05f, 0.08f, 0.14f));
                m.SetColor("_BottomColor", C(0.015f, 0.015f, 0.02f));
                m.SetColor("_HorizonGlowColor", C(0.1f, 0.1f, 0.12f));
                m.SetFloat("_HorizonGlowIntensity", 0.25f);
                m.SetFloat("_HorizonGlowFalloff", 14f);
                m.SetFloat("_StarDensity", 350f);
                m.SetFloat("_StarAmount", 0.5f);
                m.SetFloat("_StarBrightness", 6f);
                m.SetColor("_BandColor", C(0.15f, 0.16f, 0.25f));
                m.SetFloat("_BandIntensity", 1.2f);
                m.SetFloat("_MoonIntensity", 2.5f);
                m.SetFloat("_MoonSize", 2f);
                m.SetColor("_MoonGlowColor", C(0.3f, 0.4f, 0.6f));
                m.SetFloat("_MoonGlowIntensity", 0.4f);
                m.SetFloat("_CloudCoverage", 0.08f);
            },
            lighting = p =>
            {
                p.ambientSky = C(0.08f, 0.12f, 0.22f);
                p.ambientEquator = C(0.05f, 0.07f, 0.1f);
                p.ambientGround = C(0.02f, 0.02f, 0.02f);
                p.fogColor = C(0.03f, 0.05f, 0.09f);
                p.fogDensity = 0.008f;
                p.moonColor = C(0.65f, 0.75f, 1f);
                p.moonIntensity = 0.45f;
                p.moonRotation = new Vector3(30f, -130f, 0f);
                p.moonShadowStrength = 0.8f;
                p.streetLightColor = C(1f, 0.85f, 0.6f);
                p.streetLightIntensity = 10f;
                p.interiorLightColor = C(1f, 0.9f, 0.75f);
                p.interiorLightIntensity = 2.5f;
                p.accentLightColor = C(0.2f, 1f, 0.45f);
            },
            post = v =>
            {
                AddBloom(v, 1f, 0.8f, 0.65f, Color.white);
                AddTonemapping(v, TonemappingMode.ACES);
                AddColorAdjustments(v, 0f, 8f, -5f, Color.white);
                AddWhiteBalance(v, -20f, 0f);
                AddSplitToning(v, C(0.3f, 0.4f, 0.8f), C(1f, 0.9f, 0.75f), -30f);
                AddVignette(v, 0.3f, 0.4f);
                AddFilmGrain(v, FilmGrainLookup.Thin1, 0.1f);
            }
        }
    };

    public static List<NightPreset> CreateOrReset()
    {
        Shader skyShader = Shader.Find(SkyShaderName);
        if (skyShader == null)
        {
            Debug.LogError($"Night Lab: shader '{SkyShaderName}' not found. Check the console for shader compile errors.");
            return new List<NightPreset>();
        }

        var presets = new List<NightPreset>();
        foreach (Definition definition in Definitions)
        {
            string folder = $"{PresetFolder}/{definition.name}";
            EnsureFolder(folder);

            // Reuse existing assets so scene references and GUIDs survive a reset.
            string skyPath = $"{folder}/{definition.name}_Sky.mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(skyShader);
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            else
            {
                sky.shader = skyShader;
            }
            ResetMaterialToShaderDefaults(sky);
            definition.sky(sky);
            EditorUtility.SetDirty(sky);

            string volumePath = $"{folder}/{definition.name}_Volume.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(volumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, volumePath);
            }
            else
            {
                foreach (VolumeComponent component in profile.components)
                    UnityEngine.Object.DestroyImmediate(component, true);
                profile.components.Clear();
            }
            definition.post(profile);
            EditorUtility.SetDirty(profile);

            string presetPath = $"{folder}/{definition.name}.asset";
            NightPreset preset = AssetDatabase.LoadAssetAtPath<NightPreset>(presetPath);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<NightPreset>();
                AssetDatabase.CreateAsset(preset, presetPath);
            }
            definition.lighting(preset);
            preset.skybox = sky;
            preset.volumeProfile = profile;
            EditorUtility.SetDirty(preset);

            presets.Add(preset);
        }

        AssetDatabase.SaveAssets();
        return presets;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static void ResetMaterialToShaderDefaults(Material material)
    {
        var defaults = new Material(material.shader);
        material.CopyPropertiesFromMaterial(defaults);
        UnityEngine.Object.DestroyImmediate(defaults);
    }

    static T Add<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component = profile.Add<T>();
        component.name = typeof(T).Name;
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    static void AddBloom(VolumeProfile v, float threshold, float intensity, float scatter, Color tint)
    {
        var bloom = Add<Bloom>(v);
        bloom.threshold.Override(threshold);
        bloom.intensity.Override(intensity);
        bloom.scatter.Override(scatter);
        bloom.tint.Override(tint);
        bloom.highQualityFiltering.Override(true);
    }

    static void AddTonemapping(VolumeProfile v, TonemappingMode mode)
    {
        Add<Tonemapping>(v).mode.Override(mode);
    }

    static void AddColorAdjustments(VolumeProfile v, float exposure, float contrast, float saturation, Color filter)
    {
        var adjustments = Add<ColorAdjustments>(v);
        adjustments.postExposure.Override(exposure);
        adjustments.contrast.Override(contrast);
        adjustments.saturation.Override(saturation);
        adjustments.colorFilter.Override(filter);
    }

    static void AddWhiteBalance(VolumeProfile v, float temperature, float tint)
    {
        var whiteBalance = Add<WhiteBalance>(v);
        whiteBalance.temperature.Override(temperature);
        whiteBalance.tint.Override(tint);
    }

    static void AddSplitToning(VolumeProfile v, Color shadows, Color highlights, float balance)
    {
        var splitToning = Add<SplitToning>(v);
        splitToning.shadows.Override(shadows);
        splitToning.highlights.Override(highlights);
        splitToning.balance.Override(balance);
    }

    static void AddVignette(VolumeProfile v, float intensity, float smoothness)
    {
        var vignette = Add<Vignette>(v);
        vignette.color.Override(Color.black);
        vignette.intensity.Override(intensity);
        vignette.smoothness.Override(smoothness);
    }

    static void AddFilmGrain(VolumeProfile v, FilmGrainLookup type, float intensity)
    {
        var grain = Add<FilmGrain>(v);
        grain.type.Override(type);
        grain.intensity.Override(intensity);
        grain.response.Override(0.8f);
    }

    static void AddChromaticAberration(VolumeProfile v, float intensity)
    {
        Add<ChromaticAberration>(v).intensity.Override(intensity);
    }
}
