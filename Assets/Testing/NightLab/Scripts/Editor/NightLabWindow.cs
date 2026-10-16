using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Tools > Suspect Unknown > Night Lab. Generates night presets, applies them to the
/// open scene, snapshots the current look, and builds a test set of night lights.
/// </summary>
public class NightLabWindow : EditorWindow
{
    const string LightsRootName = "NightLab_Lights";
    const string SwitcherName = "NightLab_PresetSwitcher";
    const string GlowMaterialPath = NightPresetDefaults.RootFolder + "/Materials/NightLab_Glow.mat";
    const string PoleMaterialPath = NightPresetDefaults.RootFolder + "/Materials/NightLab_Pole.mat";

    NightPreset[] m_Presets = new NightPreset[0];
    GameObject m_StreetLampPrefab;
    bool m_FlickerInterior = true;
    float m_LampOutset = 1.5f;
    Vector2 m_Scroll;

    [MenuItem(MenuPaths.NightLab, false, MenuPaths.WindowsPriority)]
    static void Open()
    {
        GetWindow<NightLabWindow>("Night Lab");
    }

    void OnEnable()
    {
        RefreshPresetList();
    }

    void OnFocus()
    {
        RefreshPresetList();
    }

    void RefreshPresetList()
    {
        m_Presets = AssetDatabase.FindAssets("t:NightPreset")
            .Select(guid => AssetDatabase.LoadAssetAtPath<NightPreset>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(preset => preset != null)
            .OrderBy(preset => preset.name)
            .ToArray();
    }

    void OnGUI()
    {
        m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Apply a preset to the open scene. Tweak its sky material, volume profile and lighting values in the Inspector, then Apply again.",
            MessageType.None);

        if (GUILayout.Button("Create / Reset Default Presets"))
        {
            if (EditorUtility.DisplayDialog("Night Lab",
                    "Create the default presets, or overwrite your tweaks to them if they already exist?", "Yes", "Cancel"))
            {
                NightPresetDefaults.CreateOrReset();
                RefreshPresetList();
            }
        }

        foreach (NightPreset preset in m_Presets)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(preset.name);
                if (GUILayout.Button("Apply", GUILayout.Width(60)))
                    ApplyPreset(preset);
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeObject = preset;
                    EditorGUIUtility.PingObject(preset);
                }
            }
        }

        if (GUILayout.Button("Snapshot Current Scene As Preset"))
            SnapshotScene();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Test Lights", EditorStyles.boldLabel);
        m_StreetLampPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Street Lamp Prefab", "Optional. Leave empty for a simple generated pole."),
            m_StreetLampPrefab, typeof(GameObject), false);
        m_LampOutset = EditorGUILayout.FloatField(new GUIContent("Lamp Outset", "Distance outside the level bounds."), m_LampOutset);
        m_FlickerInterior = EditorGUILayout.Toggle("Flickering Interior Light", m_FlickerInterior);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Build Night Lights"))
                BuildLights();
            if (GUILayout.Button("Remove Night Lights"))
                RemoveLights();
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Play Mode", EditorStyles.boldLabel);
        if (GUILayout.Button("Add Preset Switcher (keys 1-9, [ ])"))
            AddSwitcher();

        EditorGUILayout.EndScrollView();
    }

    static Light FindSun()
    {
        if (RenderSettings.sun != null)
            return RenderSettings.sun;
        return FindObjectsByType<Light>(FindObjectsInactive.Exclude)
            .FirstOrDefault(l => l.type == LightType.Directional);
    }

    static Volume FindOrCreateGlobalVolume()
    {
        Volume volume = FindObjectsByType<Volume>(FindObjectsInactive.Exclude)
            .Where(v => v.isGlobal)
            .OrderByDescending(v => v.priority)
            .FirstOrDefault();
        if (volume != null)
            return volume;

        var go = new GameObject("GlobalVolume");
        Undo.RegisterCreatedObjectUndo(go, "Create Global Volume");
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        return volume;
    }

    static void ApplyPreset(NightPreset preset)
    {
        Light sun = FindSun();
        Volume volume = FindOrCreateGlobalVolume();

        var undoTargets = FindObjectsByType<NightLabLight>(FindObjectsInactive.Include)
            .SelectMany(l => new Object[] { l, l.GetComponent<Light>() })
            .Append(volume);
        if (sun != null)
            undoTargets = undoTargets.Append(sun).Append(sun.transform);
        Undo.RecordObjects(undoTargets.ToArray(), $"Apply {preset.name}");

        preset.Apply(sun, volume);

        // Post-processing does nothing unless the camera renders it.
        foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
        {
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data != null && !data.renderPostProcessing)
            {
                Undo.RecordObject(data, "Enable Post Processing");
                data.renderPostProcessing = true;
            }
        }

        if (preset.skybox != null)
            EditorUtility.SetDirty(preset.skybox);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        SceneView.RepaintAll();
        Debug.Log($"Night Lab: applied '{preset.name}'. Lighting settings (skybox, ambient, fog) have no undo; snapshot first if needed.");
    }

    void SnapshotScene()
    {
        string folder = NightPresetDefaults.PresetFolder;
        NightPresetDefaults.EnsureFolder(folder);
        string path = EditorUtility.SaveFilePanelInProject("Snapshot Preset",
            $"Snapshot_{EditorSceneManager.GetActiveScene().name}", "asset", "Save current scene look as a preset", folder);
        if (string.IsNullOrEmpty(path))
            return;

        var preset = CreateInstance<NightPreset>();
        preset.skybox = RenderSettings.skybox;
        preset.ambientMode = RenderSettings.ambientMode;
        preset.ambientSky = RenderSettings.ambientSkyColor;
        preset.ambientEquator = RenderSettings.ambientEquatorColor;
        preset.ambientGround = RenderSettings.ambientGroundColor;
        preset.ambientIntensity = RenderSettings.ambientIntensity;
        preset.reflectionIntensity = RenderSettings.reflectionIntensity;
        preset.fog = RenderSettings.fog;
        preset.fogColor = RenderSettings.fogColor;
        preset.fogDensity = RenderSettings.fogDensity;

        Light sun = FindSun();
        if (sun != null)
        {
            preset.moonColor = sun.useColorTemperature
                ? sun.color * Mathf.CorrelatedColorTemperatureToRGB(sun.colorTemperature)
                : sun.color;
            preset.moonIntensity = sun.intensity;
            preset.moonRotation = sun.transform.eulerAngles;
            preset.moonShadowStrength = sun.shadowStrength;
        }

        Volume volume = FindObjectsByType<Volume>(FindObjectsInactive.Exclude).FirstOrDefault(v => v.isGlobal);
        if (volume != null)
            preset.volumeProfile = volume.sharedProfile;

        AssetDatabase.CreateAsset(preset, path);
        AssetDatabase.SaveAssets();
        RefreshPresetList();
        EditorGUIUtility.PingObject(preset);
    }

    static Bounds GetLevelBounds()
    {
        GameObject playground = GameObject.Find("Playground");
        Renderer[] renderers = playground != null
            ? playground.GetComponentsInChildren<Renderer>()
            : FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);

        renderers = renderers.Where(r => r.GetComponentInParent<NightLabLight>() == null
                                         && !r.transform.root.name.StartsWith(LightsRootName)).ToArray();
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, new Vector3(10f, 0f, 10f));

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);
        return bounds;
    }

    void RemoveLights()
    {
        GameObject existing = GameObject.Find(LightsRootName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);
    }

    void BuildLights()
    {
        RemoveLights();

        Material glow = GetOrCreateGlowMaterial();
        Material pole = GetOrCreatePoleMaterial();
        Bounds bounds = GetLevelBounds();
        float floor = bounds.min.y;

        var root = new GameObject(LightsRootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Night Lights");

        // Street lamps just outside each corner, arms reaching toward the level.
        Vector3 extents = bounds.extents + new Vector3(m_LampOutset, 0f, m_LampOutset);
        Vector3[] corners =
        {
            new Vector3(-extents.x, 0f, -extents.z),
            new Vector3(extents.x, 0f, -extents.z),
            new Vector3(-extents.x, 0f, extents.z),
            new Vector3(extents.x, 0f, extents.z),
        };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 position = new Vector3(bounds.center.x + corners[i].x, floor, bounds.center.z + corners[i].z);
            Vector3 toCenter = new Vector3(bounds.center.x - position.x, 0f, bounds.center.z - position.z).normalized;
            BuildStreetLamp(root.transform, $"StreetLamp_{i + 1}", position, toCenter, glow, pole, i == 3);
        }

        // Interior fluorescent tube over the middle of the level.
        var tube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tube.name = "InteriorTube";
        StripCollider(tube);
        tube.transform.SetParent(root.transform);
        tube.transform.position = new Vector3(bounds.center.x, floor + 2.8f, bounds.center.z);
        tube.transform.localScale = new Vector3(1.2f, 0.05f, 0.12f);
        tube.GetComponent<Renderer>().sharedMaterial = glow;

        var tubeLightGo = new GameObject("InteriorLight");
        tubeLightGo.transform.SetParent(root.transform);
        tubeLightGo.transform.position = tube.transform.position + Vector3.down * 0.15f;
        Light tubeLight = tubeLightGo.AddComponent<Light>();
        tubeLight.type = LightType.Point;
        tubeLight.range = 7f;
        tubeLight.shadows = LightShadows.Soft;
        var tubeNightLight = tubeLightGo.AddComponent<NightLabLight>();
        tubeNightLight.Setup(NightLabLight.LightRole.Interior, tube.GetComponent<Renderer>(), 4f);
        if (m_FlickerInterior)
            tubeLightGo.AddComponent<NightLightFlicker>().Configure(NightLightFlicker.FlickerMode.Dropout, 0.4f, 0.9f);

        // Accent: a small exit sign glowing on the far edge.
        var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.name = "ExitSign";
        StripCollider(sign);
        sign.transform.SetParent(root.transform);
        sign.transform.position = new Vector3(bounds.center.x, floor + 2.2f, bounds.max.z + 0.1f);
        sign.transform.localScale = new Vector3(0.5f, 0.2f, 0.06f);
        sign.GetComponent<Renderer>().sharedMaterial = glow;

        var signLightGo = new GameObject("ExitSignLight");
        signLightGo.transform.SetParent(root.transform);
        signLightGo.transform.position = sign.transform.position + Vector3.back * 0.4f;
        Light signLight = signLightGo.AddComponent<Light>();
        signLight.type = LightType.Point;
        signLight.range = 3f;
        signLightGo.AddComponent<NightLabLight>().Setup(NightLabLight.LightRole.Accent, sign.GetComponent<Renderer>(), 5f);

        // Pick up colors from whatever look is active; default to the first preset.
        NightPreset first = m_Presets.FirstOrDefault();
        foreach (NightLabLight nightLight in root.GetComponentsInChildren<NightLabLight>())
        {
            if (first == null)
            {
                nightLight.SetColor(Color.white, 5f);
                continue;
            }
            switch (nightLight.Role)
            {
                case NightLabLight.LightRole.Street: nightLight.SetColor(first.streetLightColor, first.streetLightIntensity); break;
                case NightLabLight.LightRole.Interior: nightLight.SetColor(first.interiorLightColor, first.interiorLightIntensity); break;
                case NightLabLight.LightRole.Accent: nightLight.SetColor(first.accentLightColor, first.accentLightIntensity); break;
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
    }

    void BuildStreetLamp(Transform parent, string name, Vector3 position, Vector3 toCenter, Material glow, Material poleMaterial, bool flicker)
    {
        var lamp = new GameObject(name);
        lamp.transform.SetParent(parent);
        lamp.transform.position = position;
        lamp.transform.rotation = Quaternion.LookRotation(toCenter, Vector3.up);

        Vector3 headPosition;
        Renderer headRenderer = null;

        if (m_StreetLampPrefab != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(m_StreetLampPrefab, lamp.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            Bounds prefabBounds = new Bounds(position, Vector3.zero);
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
                prefabBounds.Encapsulate(r.bounds);
            headPosition = new Vector3(prefabBounds.center.x, prefabBounds.max.y - 0.3f, prefabBounds.center.z);
        }
        else
        {
            const float height = 4f;
            const float armLength = 1.2f;

            var poleGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            poleGo.name = "Pole";
            StripCollider(poleGo);
            poleGo.transform.SetParent(lamp.transform, false);
            poleGo.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            poleGo.transform.localScale = new Vector3(0.12f, height * 0.5f, 0.12f);
            poleGo.GetComponent<Renderer>().sharedMaterial = poleMaterial;

            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm";
            StripCollider(arm);
            arm.transform.SetParent(lamp.transform, false);
            arm.transform.localPosition = new Vector3(0f, height - 0.05f, armLength * 0.5f);
            arm.transform.localScale = new Vector3(0.08f, 0.08f, armLength);
            arm.GetComponent<Renderer>().sharedMaterial = poleMaterial;

            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            StripCollider(head);
            head.transform.SetParent(lamp.transform, false);
            head.transform.localPosition = new Vector3(0f, height - 0.12f, armLength);
            head.transform.localScale = new Vector3(0.3f, 0.08f, 0.5f);
            headRenderer = head.GetComponent<Renderer>();
            headRenderer.sharedMaterial = glow;

            headPosition = head.transform.position + Vector3.down * 0.1f;
        }

        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(lamp.transform);
        lightGo.transform.position = headPosition;
        // Point down, tilted slightly toward the level.
        lightGo.transform.rotation = Quaternion.LookRotation(Vector3.down + toCenter * 0.35f, toCenter);

        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Spot;
        light.spotAngle = 120f;
        light.innerSpotAngle = 60f;
        light.range = 12f;
        light.shadows = LightShadows.Soft;

        lightGo.AddComponent<NightLabLight>().Setup(NightLabLight.LightRole.Street, headRenderer, 8f);
        if (flicker)
            lightGo.AddComponent<NightLightFlicker>().Configure(NightLightFlicker.FlickerMode.Buzz, 0.3f, 0.35f);
    }

    static void StripCollider(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            DestroyImmediate(collider);
    }

    static Material GetOrCreateGlowMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
        if (material != null)
            return material;

        NightPresetDefaults.EnsureFolder(System.IO.Path.GetDirectoryName(GlowMaterialPath)?.Replace('\\', '/'));
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", new Color(0.2f, 0.2f, 0.2f));
        material.SetColor("_EmissionColor", Color.white * 4f);
        AssetDatabase.CreateAsset(material, GlowMaterialPath);
        // Set after CreateAsset: the keyword was lost when set before import.
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    static Material GetOrCreatePoleMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(PoleMaterialPath);
        if (material != null)
            return material;

        NightPresetDefaults.EnsureFolder(System.IO.Path.GetDirectoryName(PoleMaterialPath)?.Replace('\\', '/'));
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.13f));
        material.SetFloat("_Smoothness", 0.35f);
        material.SetFloat("_Metallic", 0.6f);
        AssetDatabase.CreateAsset(material, PoleMaterialPath);
        return material;
    }

    void AddSwitcher()
    {
        GameObject go = GameObject.Find(SwitcherName);
        if (go == null)
        {
            go = new GameObject(SwitcherName);
            Undo.RegisterCreatedObjectUndo(go, "Add Preset Switcher");
        }

        if (!go.TryGetComponent(out NightPresetSwitcher switcher))
            switcher = go.AddComponent<NightPresetSwitcher>();
        switcher.Setup(m_Presets, FindSun(), FindOrCreateGlobalVolume());
        EditorUtility.SetDirty(switcher);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = go;
    }
}
