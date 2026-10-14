using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Builds the lockpicking HUD prefab from scratch, and wires the lock camera plus a key and a rim light
// into the LockpickMinigameView prefab. The HUD is nested in PlayerHud, which picks up the rebuild by
// itself. In code rather than by hand so the layout is reviewable in git and a bad tweak is one menu
// click from undone. Safe to run again: it rebuilds the HUD and updates the lights
public static class LockpickHudBuilder
{
    private const string HudPath = "Assets/Core/Interactables/Prefabs/LockpickHud.prefab";
    private const string ViewPath = "Assets/Core/Interactables/Prefabs/LockpickMinigameView.prefab";
    private const string IconFolder = "Assets/Core/Interactables/Textures/Lockpick/";
    private const string FontPath = "Assets/Core/UI/Fonts/Electrolize-Regular SDF.asset";

    private const string KeyLightName = "LockpickKeyLight";
    private const string RimLightName = "LockpickRimLight";

    // Above the gameplay HUD (sorting order 0), so the hints draw over the lock's render texture
    private const int SortingOrder = 10;

    private static readonly Color TextColor = new Color(0.92f, 0.96f, 1f, 1f);
    private static readonly Color FaintColor = new Color(0.92f, 0.96f, 1f, 0.55f);

    [MenuItem(MenuPaths.InteractablesBuildLockpickHud, priority = MenuPaths.InteractablesPriority)]
    public static void Build()
    {
        Sprite move = LoadIcon("IconMouseMove.png");
        Sprite turn = LoadIcon("IconMouseLeft.png");
        Sprite leave = LoadIcon("IconKeyEsc.png");
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (move == null || turn == null || leave == null || font == null)
        {
            Debug.LogError($"Lockpick HUD: missing icons in {IconFolder} or the font at {FontPath}");
            return;
        }

        BuildHud(move, turn, leave, font);
        WireView();
        Debug.Log($"Lockpick HUD built at {HudPath} and wired into {ViewPath}");
    }

    private static Sprite LoadIcon(string file)
    {
        string path = IconFolder + file;
        if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
            (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void BuildHud(Sprite moveIcon, Sprite turnIcon, Sprite leaveIcon, TMP_FontAsset font)
    {
        Sprite panel = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        var root = new GameObject("LockpickHud", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        var group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        var hud = root.AddComponent<LockpickHud>();

        // Top: title, level pips and level text
        Label("Title", root, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(600f, 40f), font, 26f, "LOCKPICKING", FaintColor)
            .characterSpacing = 18f;
        var pipsRow = Rect("LevelPips", root, new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(120f, 16f));
        var pips = new Image[3];
        for (int i = 0; i < pips.Length; i++)
            pips[i] = Picture($"Pip{i + 1}", pipsRow, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 30f, 0f), new Vector2(14f, 14f), knob, TextColor);
        var level = Label("Level", root, new Vector2(0.5f, 1f), new Vector2(0f, -114f), new Vector2(300f, 28f), font, 20f, "LEVEL 1", FaintColor);

        // Right of the lock: the turn meter
        var meter = Rect("TurnMeter", root, new Vector2(0.5f, 0.5f), new Vector2(330f, 0f), new Vector2(18f, 320f));
        var track = Picture("Track", meter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 320f), panel, new Color(1f, 1f, 1f, 0.12f));
        track.type = Image.Type.Sliced;
        // A Filled image clips its sprite; without one it always draws the whole bar
        var fill = Picture("Fill", meter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 312f), panel, TextColor);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.fillAmount = 0f;
        Label("MeterLabel", root, new Vector2(0.5f, 0.5f), new Vector2(330f, -186f), new Vector2(120f, 28f), font, 18f, "TURN", FaintColor);

        // Top, under the level: the status line. Bottom: the control hints, clear of the lock
        var status = Label("Status", root, new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(1400f, 40f), font, 28f, "", TextColor);
        var moveRow = HintRow("MoveHint", root, -400f, moveIcon, "Move pick", font);
        var turnRow = HintRow("TurnHint", root, 0f, turnIcon, "Hold to turn", font);
        HintRow("LeaveHint", root, 400f, leaveIcon, "Leave", font);

        var so = new SerializedObject(hud);
        so.FindProperty("group").objectReferenceValue = group;
        so.FindProperty("moveHint").objectReferenceValue = moveRow;
        so.FindProperty("turnHint").objectReferenceValue = turnRow;
        so.FindProperty("status").objectReferenceValue = status;
        so.FindProperty("meterFill").objectReferenceValue = fill;
        so.FindProperty("levelLabel").objectReferenceValue = level;
        SerializedProperty pipList = so.FindProperty("levelPips");
        pipList.arraySize = pips.Length;
        for (int i = 0; i < pips.Length; i++)
            pipList.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, HudPath);
        Object.DestroyImmediate(root);
    }

    // Icon and label side by side, faded as one through its CanvasGroup
    private static CanvasGroup HintRow(string name, GameObject parent, float x, Sprite icon, string text, TMP_FontAsset font)
    {
        var row = Rect(name, parent, new Vector2(0.5f, 0f), new Vector2(x, 72f), new Vector2(300f, 56f));
        var group = row.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        Picture("Icon", row, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(52f, 52f), icon, TextColor);
        var label = Label("Label", row, new Vector2(0f, 0.5f), new Vector2(170f, 0f), new Vector2(220f, 40f), font, 26f, text, TextColor);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return group;
    }

    private static GameObject Rect(string name, GameObject parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent.transform, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return go;
    }

    private static Image Picture(string name, GameObject parent, Vector2 anchor, Vector2 position, Vector2 size, Sprite sprite, Color color)
    {
        var image = Rect(name, parent, anchor, position, size).AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Label(string name, GameObject parent, Vector2 anchor, Vector2 position, Vector2 size,
        TMP_FontAsset font, float fontSize, string text, Color color)
    {
        var label = Rect(name, parent, anchor, position, size).AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.text = text;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    private static void WireView()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(ViewPath);
        try
        {
            var view = contents.GetComponent<LockpickView>();
            var lockCamera = contents.GetComponentInChildren<Camera>(true);
            if (view == null || lockCamera == null)
            {
                Debug.LogError($"Lockpick HUD: {ViewPath} needs a LockpickView on its root and a camera below it");
                return;
            }

            var so = new SerializedObject(view);
            so.FindProperty("lockCamera").objectReferenceValue = lockCamera;
            so.ApplyModifiedPropertiesWithoutUndo();

            var lockRoot = so.FindProperty("lockRoot").objectReferenceValue as Transform;
            if (lockRoot != null) AddLights(lockCamera.transform, lockRoot);
            else Debug.LogWarning("Lockpick HUD: the view has no Lock Root, so no lights were added");

            PrefabUtility.SaveAsPrefabAsset(contents, ViewPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // A warm key from above-left and a cool rim from behind-right, seen only by the lock's own layer so
    // they never light the level. Placed relative to the camera and the lock, scaled by their distance
    private static void AddLights(Transform cameraTransform, Transform lockRoot)
    {
        Vector3 target = lockRoot.position;
        Vector3 forward = (target - cameraTransform.position).normalized;
        Vector3 right = cameraTransform.right;
        Vector3 up = cameraTransform.up;
        float scale = Vector3.Distance(cameraTransform.position, target) / 3.5f;
        int mask = 1 << lockRoot.gameObject.layer;

        Light key = FindOrAddLight(cameraTransform.parent, KeyLightName, lockRoot.gameObject.layer);
        key.type = LightType.Spot;
        key.spotAngle = 45f;
        key.innerSpotAngle = 25f;
        key.range = 7f * scale;
        key.intensity = 4f;
        key.color = new Color(1f, 0.86f, 0.68f);
        key.transform.position = target + (-right * 1.3f + up * 1.4f - forward * 1.2f) * scale;
        key.transform.LookAt(target);
        Finish(key, mask);

        Light rim = FindOrAddLight(cameraTransform.parent, RimLightName, lockRoot.gameObject.layer);
        rim.type = LightType.Point;
        rim.range = 3f * scale;
        rim.intensity = 2.5f;
        rim.color = new Color(0.55f, 0.72f, 1f);
        rim.transform.position = target + (right * 1.1f + up * 0.7f + forward * 0.8f) * scale;
        Finish(rim, mask);
    }

    private static Light FindOrAddLight(Transform parent, string name, int layer)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(parent, true);
        go.layer = layer;
        return go.TryGetComponent(out Light light) ? light : go.AddComponent<Light>();
    }

    private static void Finish(Light light, int mask)
    {
        light.cullingMask = mask;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;
    }
}
