using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the MainMenuConcept scene from code: a daylight safehouse with four stations
// (menu board, wardrobe, shop monitor, planning table) and a miniature night city on the
// table for the heist map. Everything is generated so the concept can be rebuilt after a
// tweak here instead of being hand-edited; it is a mock-up for inspiration, not the menu.
// Also generates the icon and border sprites and the materials it needs.
public static class MainMenuConceptBuilder
{
    private const string Root = "Assets/Testing/MainMenuConcept/";
    private const string ScenePath = Root + "MainMenuConcept.unity";
    private const string PlayerPrefab = "Assets/Player/Player.prefab";
    private const string FontPath = "Assets/Core/UI/Fonts/Electrolize-Regular SDF.asset";
    private const string SyntyCity = "Assets/SyntyStudios/PolygonCity/Prefabs/";
    private const string DaySky = "Assets/AllSkyFree/Cartoon Base BlueSky/Day_BlueSky_Nothing.mat";
    private const string NightSky = "Assets/AllSkyFree/Cartoon Base NightSky/Cartoon Base NightSky.mat";

    // Palette shared by every panel
    private static readonly Color Amber = new Color(0.95f, 0.70f, 0.24f);
    private static readonly Color Ink = new Color(0.07f, 0.08f, 0.09f, 0.86f);
    private static readonly Color Muted = new Color(0.78f, 0.78f, 0.78f);
    private static readonly Color Paper = new Color(0.95f, 0.93f, 0.88f);

    // Where each station lives in the room, metres
    private static readonly Vector3 TableCenter = new Vector3(1.4f, 0f, 1.7f);
    private const float TableTop = 0.86f;
    private static readonly Vector3 MainCamPos = new Vector3(0.55f, 1.7f, -4.4f);
    private static readonly Vector3 MainCamLook = new Vector3(0.6f, 1.3f, 1.6f);
    private static readonly Vector3 WardrobeSpot = new Vector3(4.5f, 0f, 2.9f);
    private static readonly Vector3 WardrobeCamPos = new Vector3(4.5f, 1.45f, -0.35f);
    private static readonly Vector3 MonitorPos = new Vector3(-3.7f, 1.9f, 4.9f);
    private static readonly Vector3 ShopCamPos = new Vector3(-3.7f, 1.7f, 2.75f);

    private static TMP_FontAsset font;
    private static Sprite borderSprite, roundSprite;
    private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    private static Camera mainCam;
    private static MainMenuConcept menu;

    [MenuItem(MenuPaths.MainMenuBuildConcept, priority = MenuPaths.MainMenuPriority)]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("MainMenuConceptBuilder: leave play mode before building");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError($"MainMenuConceptBuilder: font not found at {FontPath}");
            return;
        }
        BuildSprites();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Light sun = BuildLighting(out Light hangingLamp);
        BuildRoom();
        Transform cityRoot = BuildCity(out List<Light> streetLights, out List<MapLocation> locations, out Transform labelRoot);
        PlayerCustomization player = BuildPlayer();
        if (player == null) return;
        BuildCameras(out CinemachineCamera camMain, out CinemachineCamera camWardrobe, out CinemachineCamera camShop, out CinemachineCamera camMap);
        BuildPostProcessing();

        var es = new GameObject("EventSystem", typeof(EventSystem));
        es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

        var managers = new GameObject("MenuConcept");
        menu = managers.AddComponent<MainMenuConcept>();
        Wallet wallet = managers.AddComponent<Wallet>();
        DayNightBlend dayNight = managers.AddComponent<DayNightBlend>();

        CanvasGroup mainGroup, settingsGroup;
        BuildBoard(out mainGroup, out settingsGroup);
        CanvasGroup wardLeft, wardRight;
        BuildWardrobeUi(player, out wardLeft, out wardRight);
        CanvasGroup shopGroup = BuildShopUi(wallet);
        CanvasGroup mapGroup = BuildHud(wallet, locations, labelRoot.gameObject, out MapBoard board);

        foreach (MapLocation location in locations) Set(location, "board", board);

        streetLights.Add(hangingLamp);
        Set(dayNight, "sun", sun);
        Set(dayNight, "day", new DayNightBlend.Look
        {
            lightColor = new Color(1f, 0.9f, 0.74f), lightIntensity = 2.8f, lightEuler = new Vector3(38f, 135f, 0f),
            ambientSky = new Color(0.95f, 1.0f, 1.15f), ambientEquator = new Color(1.0f, 0.93f, 0.82f), ambientGround = new Color(0.5f, 0.43f, 0.36f),
            skybox = AssetDatabase.LoadAssetAtPath<Material>(DaySky)
        });
        Set(dayNight, "night", new DayNightBlend.Look
        {
            lightColor = new Color(0.4f, 0.52f, 1f), lightIntensity = 0.9f, lightEuler = new Vector3(45f, 205f, 0f),
            ambientSky = new Color(0.16f, 0.22f, 0.45f), ambientEquator = new Color(0.10f, 0.12f, 0.26f), ambientGround = new Color(0.03f, 0.03f, 0.05f),
            skybox = AssetDatabase.LoadAssetAtPath<Material>(NightSky)
        });
        Set(dayNight, "nightLights", streetLights.ToArray());

        Set(menu, "stations", new[]
        {
            new MainMenuConcept.StationView { station = MenuStation.Main, camera = camMain, panels = new[] { mainGroup } },
            new MainMenuConcept.StationView { station = MenuStation.Settings, camera = camMain, panels = new[] { settingsGroup } },
            new MainMenuConcept.StationView { station = MenuStation.Wardrobe, camera = camWardrobe, panels = new[] { wardLeft, wardRight } },
            new MainMenuConcept.StationView { station = MenuStation.Shop, camera = camShop, panels = new[] { shopGroup } },
            new MainMenuConcept.StationView { station = MenuStation.Map, camera = camMap, panels = new[] { mapGroup } },
        });
        Set(menu, "dayNight", dayNight);
        Set(menu, "mapBoard", board);

        // Start in daylight in the editor too, so the saved scene looks like the menu
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(DaySky);
        foreach (Light l in streetLights) l.enabled = false;

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        Debug.Log($"MainMenuConceptBuilder: built {ScenePath}");
    }

    // ---------------------------------------------------------------- lighting and room

    private static Light BuildLighting(out Light hangingLamp)
    {
        var sunGo = new GameObject("Sun");
        Light sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.9f, 0.74f);
        sun.intensity = 2.8f;
        sun.shadows = LightShadows.Soft;
        sunGo.transform.rotation = Quaternion.Euler(38f, 135f, 0f);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.95f, 1.0f, 1.15f);
        RenderSettings.ambientEquatorColor = new Color(1.0f, 0.93f, 0.82f);
        RenderSettings.ambientGroundColor = new Color(0.5f, 0.43f, 0.36f);
        RenderSettings.sun = sun;

        var lampGo = new GameObject("HangingLamp (night)");
        lampGo.transform.position = TableCenter + new Vector3(0f, 2.35f, 0f);
        hangingLamp = lampGo.AddComponent<Light>();
        hangingLamp.type = LightType.Point;
        hangingLamp.color = new Color(1f, 0.78f, 0.5f);
        hangingLamp.intensity = 0.5f;
        hangingLamp.range = 6f;
        return sun;
    }

    private static void BuildRoom()
    {
        Material floor = Mat("Floor_Wood", new Color(0.56f, 0.40f, 0.28f), 0.35f);
        Material wall = Mat("Wall_Plaster", new Color(0.88f, 0.84f, 0.76f), 0.1f);
        Material trim = Mat("Trim_Dark", new Color(0.18f, 0.18f, 0.2f), 0.3f);
        Material wood = Mat("Wood", new Color(0.45f, 0.30f, 0.19f), 0.3f);
        Material metal = Mat("Metal", new Color(0.32f, 0.34f, 0.37f), 0.6f);
        Material cork = Mat("Cork", new Color(0.66f, 0.50f, 0.33f), 0.05f);
        Material fabric = Mat("Fabric_Rug", new Color(0.62f, 0.27f, 0.20f), 0f);
        Material mirror = Mat("Mirror", new Color(0.75f, 0.85f, 0.9f), 0.95f, metallic: 1f);
        Material screen = Mat("Screen_Off", new Color(0.03f, 0.03f, 0.04f), 0.8f);
        Material glass = Mat("Lamp_Glow", new Color(1f, 0.85f, 0.6f), 0.5f, emission: new Color(1f, 0.8f, 0.5f) * 2f);

        Transform room = new GameObject("Room").transform;

        // Shell: open towards the camera, windows in the left wall let the sun in
        Box("Floor", room, new Vector3(0f, -0.05f, -0.5f), new Vector3(14f, 0.1f, 11f), floor);
        Box("Ceiling", room, new Vector3(0f, 3.45f, -0.5f), new Vector3(14f, 0.1f, 11f), wall);
        // Back wall: a wide window behind the table, so the main view is full of daylight
        const float winL = -1.6f, winR = 3.1f, sill = 0.9f, lintel = 2.75f;
        Box("Wall_Back_L", room, new Vector3((-7f + winL) / 2f, 1.7f, 5.05f), new Vector3(winL + 7f, 3.4f, 0.1f), wall);
        Box("Wall_Back_R", room, new Vector3((7f + winR) / 2f, 1.7f, 5.05f), new Vector3(7f - winR, 3.4f, 0.1f), wall);
        Box("Wall_Back_Sill", room, new Vector3((winL + winR) / 2f, sill / 2f, 5.05f), new Vector3(winR - winL, sill, 0.1f), wall);
        Box("Wall_Back_Lintel", room, new Vector3((winL + winR) / 2f, (lintel + 3.4f) / 2f, 5.05f), new Vector3(winR - winL, 3.4f - lintel, 0.1f), wall);
        Box("Window_Ledge", room, new Vector3((winL + winR) / 2f, sill, 4.98f), new Vector3(winR - winL + 0.1f, 0.04f, 0.2f), trim);
        for (float x = winL; x <= winR + 0.01f; x += (winR - winL) / 4f)
            Box("Mullion", room, new Vector3(x, (sill + lintel) / 2f, 5.05f), new Vector3(0.06f, lintel - sill, 0.08f), trim);
        Box("Transom", room, new Vector3((winL + winR) / 2f, 2.2f, 5.05f), new Vector3(winR - winL, 0.05f, 0.08f), trim);

        // What the window looks out on: the city by day, the same city the map shows at night
        Transform outside = new GameObject("Outside").transform;
        outside.SetParent(room);
        Box("Street", outside, new Vector3(0f, -0.2f, 40f), new Vector3(120f, 0.1f, 70f), Mat("Street_Outside", new Color(0.55f, 0.56f, 0.55f), 0.1f));
        Prefab("Buildings/SM_Bld_OfficeOld_Large_01", outside, new Vector3(-14f, -0.15f, 60f), 0f, fitToLot: true);
        Prefab("Buildings/SM_Bld_OfficeOld_Small_02", outside, new Vector3(6f, -0.15f, 52f), 0f, fitToLot: true);
        Prefab("Buildings/SM_Bld_OfficeSquare_01", outside, new Vector3(24f, -0.15f, 70f), 0f, fitToLot: true);
        Box("Wall_Right", room, new Vector3(6.55f, 1.7f, -0.5f), new Vector3(0.1f, 3.4f, 11f), wall);
        Box("Skirting_Back", room, new Vector3(0f, 0.06f, 4.98f), new Vector3(14f, 0.12f, 0.04f), trim);
        Box("Skirting_Right", room, new Vector3(6.48f, 0.06f, -0.5f), new Vector3(0.04f, 0.12f, 11f), trim);

        // Left wall: sill, lintel and pillars, leaving three tall windows
        Transform left = new GameObject("Wall_Left_Windows").transform;
        left.SetParent(room);
        Box("Sill", left, new Vector3(-6.55f, 0.45f, -0.5f), new Vector3(0.1f, 0.9f, 11f), wall);
        Box("Lintel", left, new Vector3(-6.55f, 3.05f, -0.5f), new Vector3(0.1f, 0.8f, 11f), wall);
        float[] pillars = { -5.6f, -3.0f, -0.6f, 1.8f, 4.2f };
        foreach (float z in pillars) Box("Pillar", left, new Vector3(-6.55f, 1.75f, z), new Vector3(0.1f, 0.9f + 0.8f, 0.6f), wall);
        Box("Pillar_End", left, new Vector3(-6.55f, 1.75f, 4.75f), new Vector3(0.1f, 1.7f, 0.6f), wall);
        foreach (float z in new[] { -4.3f, -1.8f, 0.6f, 3.0f })
        {
            Box("Mullion_V", left, new Vector3(-6.55f, 1.75f, z), new Vector3(0.06f, 1.7f, 0.05f), trim);
            Box("Mullion_H", left, new Vector3(-6.55f, 1.8f, z), new Vector3(0.06f, 0.05f, 1.8f), trim);
        }
        Box("Floor_Outside", room, new Vector3(-12f, -0.3f, 0f), new Vector3(10f, 0.1f, 20f), Mat("Street_Outside", new Color(0.55f, 0.56f, 0.55f), 0.1f));

        Box("Rug", room, TableCenter + new Vector3(0f, 0.005f, 0f), new Vector3(4.2f, 0.01f, 3.0f), fabric);

        // Planning table: the city map lives on top of it
        Transform table = new GameObject("PlanningTable").transform;
        table.SetParent(room);
        Box("Top", table, TableCenter + new Vector3(0f, TableTop - 0.03f, 0f), new Vector3(3.0f, 0.06f, 2.0f), wood);
        foreach (Vector2 c in new[] { new Vector2(-1.4f, -0.9f), new Vector2(1.4f, -0.9f), new Vector2(-1.4f, 0.9f), new Vector2(1.4f, 0.9f) })
            Box("Leg", table, TableCenter + new Vector3(c.x, (TableTop - 0.06f) / 2f, c.y), new Vector3(0.08f, TableTop - 0.06f, 0.08f), wood);
        Box("Lamp_Cord", table, TableCenter + new Vector3(0f, 2.9f, 0f), new Vector3(0.01f, 1.1f, 0.01f), trim);
        Box("Lamp_Shade", table, TableCenter + new Vector3(0f, 2.36f, 0f), new Vector3(0.45f, 0.08f, 0.45f), metal);
        Box("Lamp_Bulb", table, TableCenter + new Vector3(0f, 2.31f, 0f), new Vector3(0.18f, 0.04f, 0.18f), glass);

        // Menu board on a stand, between camera and table
        // (the board face itself is built with its canvas in BuildBoard)

        // Wardrobe corner
        Transform ward = new GameObject("WardrobeCorner").transform;
        ward.SetParent(room);
        Vector3 cab = new Vector3(WardrobeSpot.x, 0f, 4.62f);
        Box("Cabinet_Back", ward, cab + new Vector3(0f, 1.1f, 0.2f), new Vector3(2.1f, 2.2f, 0.06f), wood);
        Box("Cabinet_Side_L", ward, cab + new Vector3(-1.02f, 1.1f, -0.1f), new Vector3(0.06f, 2.2f, 0.6f), wood);
        Box("Cabinet_Side_R", ward, cab + new Vector3(1.02f, 1.1f, -0.1f), new Vector3(0.06f, 2.2f, 0.6f), wood);
        Box("Cabinet_Top", ward, cab + new Vector3(0f, 2.2f, -0.1f), new Vector3(2.1f, 0.06f, 0.6f), wood);
        Box("Cabinet_Bottom", ward, cab + new Vector3(0f, 0.05f, -0.1f), new Vector3(2.1f, 0.1f, 0.6f), wood);
        Box("Cabinet_Door_L", ward, cab + new Vector3(-1.45f, 1.1f, -0.55f), new Vector3(0.04f, 2.1f, 0.95f), wood, new Vector3(0f, -20f, 0f));
        Box("Cabinet_Door_R", ward, cab + new Vector3(1.45f, 1.1f, -0.55f), new Vector3(0.04f, 2.1f, 0.95f), wood, new Vector3(0f, 20f, 0f));
        Box("Rail", ward, cab + new Vector3(0f, 1.95f, -0.1f), new Vector3(1.9f, 0.03f, 0.03f), metal);
        Color[] clothes =
        {
            new Color(0.75f, 0.15f, 0.12f), new Color(0.2f, 0.35f, 0.22f), new Color(0.15f, 0.2f, 0.35f),
            new Color(0.75f, 0.68f, 0.52f), new Color(0.12f, 0.12f, 0.13f), new Color(0.55f, 0.55f, 0.58f), new Color(0.6f, 0.45f, 0.25f)
        };
        for (int i = 0; i < clothes.Length; i++)
            Box("Hanging_" + i, ward, cab + new Vector3(-0.8f + i * 0.26f, 1.5f, -0.1f), new Vector3(0.08f, 0.8f, 0.45f), Mat("Cloth_" + i, clothes[i], 0f));
        Box("Mirror_Frame", ward, new Vector3(WardrobeSpot.x + 1.75f, 1.0f, 3.9f), new Vector3(0.7f, 1.8f, 0.06f), wood, new Vector3(0f, -25f, 0f));
        Box("Mirror_Glass", ward, new Vector3(WardrobeSpot.x + 1.73f, 1.0f, 3.86f), new Vector3(0.6f, 1.7f, 0.02f), mirror, new Vector3(0f, -25f, 0f));
        Cylinder("Pedestal", ward, WardrobeSpot + new Vector3(0f, 0.04f, 0f), new Vector3(0.9f, 0.04f, 0.9f), trim);

        // Shop corner: workbench, wall monitor, pegboard
        Transform shop = new GameObject("ShopCorner").transform;
        shop.SetParent(room);
        Vector3 bench = new Vector3(MonitorPos.x, 0f, 4.55f);
        Box("Bench_Top", shop, bench + new Vector3(0f, 0.92f, 0f), new Vector3(2.6f, 0.06f, 0.8f), wood);
        foreach (float x in new[] { -1.2f, 1.2f })
            Box("Bench_Leg", shop, bench + new Vector3(x, 0.45f, 0f), new Vector3(0.08f, 0.9f, 0.7f), metal);
        Box("Bench_Shelf", shop, bench + new Vector3(0f, 0.25f, 0f), new Vector3(2.4f, 0.04f, 0.7f), wood);
        Box("Monitor_Frame", shop, MonitorPos + new Vector3(0f, 0f, 0.06f), new Vector3(1.86f, 1.16f, 0.06f), trim);
        Box("Monitor_Screen", shop, MonitorPos + new Vector3(0f, 0f, 0.03f), new Vector3(1.8f, 1.1f, 0.01f), screen);
        Box("Pegboard", shop, new Vector3(MonitorPos.x - 1.75f, 1.75f, 4.97f), new Vector3(1.2f, 1.2f, 0.03f), cork);
        Box("Laptop_Base", shop, bench + new Vector3(0.7f, 0.96f, -0.05f), new Vector3(0.4f, 0.02f, 0.28f), metal);
        Box("Laptop_Lid", shop, bench + new Vector3(0.7f, 1.09f, 0.1f), new Vector3(0.4f, 0.26f, 0.02f), metal, new Vector3(-12f, 0f, 0f));
        Box("Toolbox", shop, bench + new Vector3(-0.8f, 1.03f, 0f), new Vector3(0.5f, 0.16f, 0.24f), Mat("Toolbox_Red", new Color(0.7f, 0.15f, 0.12f), 0.4f));

        // Dressing from the Synty city pack
        Prefab("Props/SM_Prop_Couch_01", room, new Vector3(-4.3f, 0f, -0.6f), 90f);
        Prefab("Props/SM_Prop_CardboardBox_01", room, new Vector3(5.8f, 0f, -0.8f), 15f);
        Prefab("Props/SM_Prop_CardboardBox_02", room, new Vector3(5.9f, 0.0f, -1.6f), -10f);
        Prefab("Props/SM_Prop_CardboardBox_03", room, new Vector3(-5.9f, 0f, 3.9f), 30f);
        Prefab("Props/SM_Prop_PotPlant_01", room, new Vector3(-6.0f, 0f, -2.4f), 0f);
        Prefab("Props/SM_Prop_PotPlant_02", room, new Vector3(-1.9f, 0f, 4.6f), 0f);
        Prefab("Props/SM_Prop_Pallet_01", room, new Vector3(6.0f, 0f, 0.6f), 90f);
    }

    // ---------------------------------------------------------------- the night city on the table

    private static Transform BuildCity(out List<Light> lights, out List<MapLocation> locations, out Transform labelRoot)
    {
        // Built at full scale around the origin, then shrunk onto the table at the end,
        // so prefab placement can use real metres and real bounds
        Transform city = new GameObject("CityMap").transform;
        Material grass = Mat("City_Grass", new Color(0.24f, 0.40f, 0.26f), 0.05f);
        Material asphalt = Mat("City_Asphalt", new Color(0.20f, 0.21f, 0.25f), 0.2f);
        Material pavement = Mat("City_Pavement", new Color(0.58f, 0.59f, 0.62f), 0.1f);
        Material paint = Mat("City_Paint", new Color(0.9f, 0.9f, 0.88f), 0.1f);
        Material board = Mat("City_Board", new Color(0.30f, 0.21f, 0.14f), 0.3f);
        Material pole = Mat("City_Pole", new Color(0.18f, 0.19f, 0.21f), 0.5f);
        Material bulb = Mat("City_Bulb", new Color(1f, 0.85f, 0.55f), 0.5f, emission: new Color(1f, 0.75f, 0.4f) * 4f);

        // Compact on purpose: a readable board game, not a to-scale city. Shops are
        // enlarged so they read next to the bank from the map camera
        const float W = 96f, D = 64f, Shop = 1.8f;
        Box("Base", city, new Vector3(0f, -1.6f, 0f), new Vector3(W + 4f, 3f, D + 4f), board);
        Box("Grass", city, new Vector3(0f, -0.05f, 0f), new Vector3(W, 0.1f, D), grass);

        // One cross of roads; each quarter is a block with a pavement apron
        Box("Road_EW", city, new Vector3(0f, 0.01f, 0f), new Vector3(W, 0.04f, 12f), asphalt);
        Box("Road_NS", city, new Vector3(0f, 0.012f, 0f), new Vector3(12f, 0.04f, D), asphalt);
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
            {
                float bx = sx * (W / 4f + 3f), bz = sz * (D / 4f + 3f);
                Box("Pavement", city, new Vector3(bx, 0.08f, sz * 7.5f), new Vector3(W / 2f - 6f, 0.16f, 3f), pavement);
                Box("Pavement", city, new Vector3(sx * 7.5f, 0.08f, bz), new Vector3(3f, 0.16f, D / 2f - 6f), pavement);
            }
        for (int i = 0; i < 6; i++)
        {
            Box("Crossing", city, new Vector3(-4.5f + i * 1.8f, 0.04f, 9.5f), new Vector3(0.9f, 0.02f, 3f), paint);
            Box("Crossing", city, new Vector3(9.5f, 0.04f, -4.5f + i * 1.8f), new Vector3(3f, 0.02f, 0.9f), paint);
        }
        for (float x = -44f; x < 44f; x += 8f)
            if (Mathf.Abs(x) > 8f) Box("Lane", city, new Vector3(x, 0.035f, 0f), new Vector3(3.5f, 0.02f, 0.3f), paint);
        for (float z = -28f; z < 28f; z += 8f)
            if (Mathf.Abs(z) > 8f) Box("Lane", city, new Vector3(0f, 0.035f, z), new Vector3(0.3f, 0.02f, 3.5f), paint);

        // Targets (the four from the concept art) and filler
        locations = new List<MapLocation>();
        labelRoot = new GameObject("Labels").transform;
        labelRoot.SetParent(city);

        locations.Add(Target(city, "Buildings/SM_Bld_OfficeOld_Small_01", new Vector3(-36f, 0f, 19f), 0f, 0.75f,
            "Apartment", "Burglary, upper floors", 1, "No guards. Nosy neighbours", 1800,
            "Neighbours here have seen a grey beanie before. Low confidence."));
        locations.Add(Target(city, "Buildings/SM_Bld_Shop_03", new Vector3(-17f, 0f, 16f), 0f, Shop,
            "Thrift Store", "Cheap job. Good for shaping the profile", 1, "1 tired clerk", 900,
            "Perfect burn job: wear the red jacket here on purpose."));
        locations.Add(Target(city, "Buildings/SM_Bld_Shop_Corner_01", new Vector3(15f, 0f, 16f), 0f, Shop,
            "Jewelry Store", "Display cases and a back safe", 3, "1 guard, 2 cameras", 7500,
            "Guard here confirmed a balaclava last week. Burn it."));
        locations.Add(Target(city, "Buildings/SM_Bld_CityHall_01", new Vector3(34f, 0f, 20f), 0f, 1f,
            "Bank", "The big score. Vault behind two locks", 5, "3 guards, 6 cameras, alarm", 48000,
            "Security has adapted: patrols now favour the vault corridor."));

        Prefab("Buildings/SM_Bld_Shop_04", city, new Vector3(-17f, 0.02f, -15f), 180f, fitToLot: true, scale: Shop);
        Prefab("Buildings/SM_Bld_Shop_05", city, new Vector3(-36f, 0.02f, -15f), 180f, fitToLot: true, scale: Shop);
        Prefab("Buildings/SM_Bld_Shop_Corner_02", city, new Vector3(16f, 0.02f, -15f), 180f, fitToLot: true, scale: Shop);
        Prefab("Buildings/SM_Bld_Shop_06", city, new Vector3(36f, 0.02f, -15f), 180f, fitToLot: true, scale: Shop);

        Vector3[] trees =
        {
            new Vector3(-26f, 0f, 27f), new Vector3(-44f, 0f, 28f), new Vector3(-6f, 0f, 27f), new Vector3(22f, 0f, 28f),
            new Vector3(45f, 0f, 28f), new Vector3(-44f, 0f, -27f), new Vector3(-26f, 0f, -27f), new Vector3(-6f, 0f, -26f),
            new Vector3(26f, 0f, -27f), new Vector3(45f, 0f, -26f), new Vector3(6f, 0f, -27f), new Vector3(-26f, 0f, 20f)
        };
        foreach (Vector3 t in trees) Prefab("Environments/SM_Env_Tree_01", city, t, t.x * 7f, scale: 1.4f);

        Prefab("Vehicles/SM_Veh_Car_Sedan_01", city, new Vector3(-26f, 0.05f, -3f), 90f, scale: 1.3f);
        Prefab("Vehicles/SM_Veh_Car_Taxi_01", city, new Vector3(22f, 0.05f, 3f), -90f, scale: 1.3f);
        Prefab("Vehicles/SM_Veh_Car_Police_01", city, new Vector3(3f, 0.05f, -20f), 0f, scale: 1.3f);
        Prefab("Vehicles/SM_Veh_Car_Small_01", city, new Vector3(-3f, 0.05f, 22f), 180f, scale: 1.3f);

        // Street lamps: a pole, a glowing head and, after scaling, a tiny point light
        var lampHeads = new List<Vector3>();
        foreach (Vector3 p in new[]
        {
            new Vector3(-9.5f, 0f, 9.5f), new Vector3(9.5f, 0f, 9.5f), new Vector3(-9.5f, 0f, -9.5f), new Vector3(9.5f, 0f, -9.5f),
            new Vector3(-30f, 0f, 9.5f), new Vector3(30f, 0f, 9.5f), new Vector3(-30f, 0f, -9.5f), new Vector3(30f, 0f, -9.5f)
        })
        {
            Box("LampPole", city, p + new Vector3(0f, 3f, 0f), new Vector3(0.35f, 6f, 0.35f), pole);
            Box("LampHead", city, p + new Vector3(0f, 6.1f, 0f), new Vector3(1.0f, 0.4f, 1.0f), bulb);
            lampHeads.Add(p + new Vector3(0f, 5.4f, 0f));
        }

        // Shrink onto the table
        const float scale = 0.026f;
        city.position = TableCenter + new Vector3(0f, TableTop + 0.06f, 0f);
        city.localScale = Vector3.one * scale;

        lights = new List<Light>();
        foreach (Vector3 head in lampHeads)
        {
            var go = new GameObject("StreetLight (night)");
            go.transform.SetParent(city, false);
            go.transform.localPosition = head;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.75f, 0.42f);
            l.range = 0.16f;
            l.intensity = 0.1f;
            lights.Add(l);
        }

        // Outlines and labels are sized in world units, so they are finished after scaling
        Quaternion mapView = Quaternion.LookRotation(MapLook - MapCamPos(), Vector3.up);
        foreach (MapLocation location in locations) FinishTarget(location, labelRoot, mapView);
        return city;
    }

    private static Vector3 MapCenter => TableCenter + new Vector3(0f, TableTop, 0f);
    private static Vector3 MapLook => MapCenter + Quaternion.Euler(0f, 28f, 0f) * new Vector3(0.38f, 0f, 0.1f);
    private static Vector3 MapCamPos() => MapLook - Quaternion.Euler(52f, 28f, 0f) * Vector3.forward * 3.0f;

    private static MapLocation Target(Transform city, string prefab, Vector3 lot, float yaw, float scale, string name, string job, int difficulty,
        string security, int take, string profile)
    {
        var root = new GameObject("Target_" + name.Replace(" ", ""));
        root.transform.SetParent(city);
        root.transform.position = lot;
        GameObject building = Prefab(prefab, root.transform, lot + new Vector3(0f, 0.02f, 0f), yaw, fitToLot: true, scale: scale);

        Bounds b = WorldBounds(building);
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = b.center - lot;
        box.size = b.size;

        MapLocation location = root.AddComponent<MapLocation>();
        Set(location, "displayName", name);
        Set(location, "jobType", job);
        Set(location, "difficulty", difficulty);
        Set(location, "security", security);
        Set(location, "estimatedTake", take);
        Set(location, "profileNote", profile);
        return location;
    }

    private static void FinishTarget(MapLocation location, Transform labelRoot, Quaternion view)
    {
        BoxCollider box = location.GetComponent<BoxCollider>();
        Vector3 c = box.center, e = box.size * 0.5f;
        e.x += 1.2f; e.z += 1.2f;
        float y0 = c.y - e.y + 0.25f, y1 = c.y + e.y + 0.6f;
        Vector3 P(float sx, float y, float sz) => new Vector3(c.x + sx * e.x, y, c.z + sz * e.z);
        Vector3[] path =
        {
            P(-1, y0, -1), P(1, y0, -1), P(1, y0, 1), P(-1, y0, 1), P(-1, y0, -1),
            P(-1, y1, -1), P(1, y1, -1), P(1, y0, -1), P(1, y1, -1), P(1, y1, 1), P(1, y0, 1),
            P(1, y1, 1), P(-1, y1, 1), P(-1, y0, 1), P(-1, y1, 1), P(-1, y1, -1)
        };
        LineRenderer line = location.gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = path.Length;
        line.SetPositions(path);
        line.widthMultiplier = 0.005f;
        line.numCornerVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.sharedMaterial = Unlit("Outline_Glow", new Color(1f, 0.9f, 0.1f) * 3.5f);
        line.enabled = false;
        Set(location, "outline", line);

        // Speech-bubble label above the roof, facing the map camera
        Vector3 top = location.transform.TransformPoint(new Vector3(c.x, y1 + 3f, c.z));
        Canvas canvas = WorldCanvas("Label_" + location.DisplayName, labelRoot, top, view, new Vector2(340f, 84f), 0.0009f, interactive: false);
        canvas.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
        Image bubble = Img(canvas.transform, "Bubble", Paper, roundSprite);
        Stretch(bubble.rectTransform, 0f, 0f, 0f, 14f);
        Image tail = Img(canvas.transform, "Tail", Paper);
        RectTransform tr = tail.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0f);
        tr.sizeDelta = new Vector2(20f, 20f);
        tr.anchoredPosition = new Vector2(0f, 14f);
        tr.localRotation = Quaternion.Euler(0f, 0f, 45f);
        TMP_Text label = Txt(bubble.transform, "Text", location.DisplayName.ToUpperInvariant(), 36f, new Color(0.08f, 0.08f, 0.1f), TextAlignmentOptions.Center);
        Stretch(label.rectTransform, 12f, 4f, 12f, 4f);
        label.fontStyle = FontStyles.Bold;
    }

    // ---------------------------------------------------------------- the player on the pedestal

    // The real player prefab, so the wardrobe edits the same PlayerCustomization the game
    // loads. Everything that would make it play (input, movement, aim, perception, foot IK)
    // is switched off as scene overrides; only these stay on, for the idle pose and the looks
    private static readonly string[] PlayerKeeps = { nameof(Animator), nameof(PlayerCustomization) };

    private static PlayerCustomization BuildPlayer()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        if (asset == null)
        {
            Debug.LogError($"MainMenuConceptBuilder: player prefab not found at {PlayerPrefab}");
            return null;
        }

        var player = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        player.name = "Player (wardrobe)";
        Vector3 facing = WardrobeCamPos - WardrobeSpot;
        facing.y = 0f;
        player.transform.SetPositionAndRotation(WardrobeSpot + new Vector3(0f, 0.08f, 0f), Quaternion.LookRotation(facing));

        foreach (Behaviour b in player.GetComponentsInChildren<Behaviour>(true))
        {
            if (Array.IndexOf(PlayerKeeps, b.GetType().Name) >= 0) continue;
            b.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(b);
        }
        // CharacterController and the bone perception colliders are Colliders, not Behaviours
        foreach (Collider c in player.GetComponentsInChildren<Collider>(true))
        {
            c.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }

        var spin = new GameObject("Turntable");
        spin.transform.position = WardrobeSpot + new Vector3(0f, 1f, 0f);
        CapsuleCollider capsule = spin.AddComponent<CapsuleCollider>();
        capsule.radius = 0.45f;
        capsule.height = 2.1f;
        Set(spin.AddComponent<PlayerTurntable>(), "target", player.transform);

        return player.GetComponent<PlayerCustomization>();
    }

    // ---------------------------------------------------------------- cameras and post

    private static void BuildCameras(out CinemachineCamera main, out CinemachineCamera wardrobe, out CinemachineCamera shop, out CinemachineCamera map)
    {
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        mainCam = camGo.GetComponent<Camera>();
        mainCam.nearClipPlane = 0.03f;
        mainCam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        mainCam.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        PhysicsRaycaster ray = camGo.AddComponent<PhysicsRaycaster>();
        ray.eventMask = 1 << 0;
        CinemachineBrain brain = camGo.AddComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.3f);
        brain.IgnoreTimeScale = true;
        camGo.transform.SetPositionAndRotation(MainCamPos, Quaternion.LookRotation(MainCamLook - MainCamPos));

        main = VCam("CM Main", MainCamPos, MainCamLook, 46f);
        wardrobe = VCam("CM Wardrobe", WardrobeCamPos, WardrobeSpot + new Vector3(0f, 1.05f, 0f), 44f);
        shop = VCam("CM Shop", ShopCamPos, MonitorPos + new Vector3(0f, -0.12f, 0f), 44f);
        map = VCam("CM Map", MapCamPos(), MapLook, 40f);
    }

    private static CinemachineCamera VCam(string name, Vector3 pos, Vector3 look, float fov)
    {
        var go = new GameObject(name);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
        CinemachineCamera cam = go.AddComponent<CinemachineCamera>();
        LensSettings lens = LensSettings.Default;
        lens.FieldOfView = fov;
        lens.NearClipPlane = 0.03f;
        lens.FarClipPlane = 300f;
        cam.Lens = lens;
        return cam;
    }

    private static void BuildPostProcessing()
    {
        string path = Root + "MainMenuConcept_Volume.asset";
        AssetDatabase.DeleteAsset(path);
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.65f);
        Tonemapping tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.3f);
        color.contrast.Override(8f);
        color.saturation.Override(8f);
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.22f);
        foreach (VolumeComponent c in profile.components)
        {
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
        }
        AssetDatabase.SaveAssets();

        var go = new GameObject("Global Volume");
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    // ---------------------------------------------------------------- menu board (main + settings)

    private static void BuildBoard(out CanvasGroup mainGroup, out CanvasGroup settingsGroup)
    {
        Vector3 boardPos = new Vector3(-0.8f, 1.45f, -1.1f);
        Vector3 flat = boardPos - MainCamPos;
        flat.y = 0f;
        Quaternion rot = Quaternion.LookRotation(flat.normalized);

        // Physical cork board on a stand
        Transform stand = new GameObject("MenuBoard").transform;
        stand.SetPositionAndRotation(boardPos, rot);
        Material wood = Mat("Wood", Color.white, 0f);
        Material cork = Mat("Cork", Color.white, 0f);
        Material metal = Mat("Metal", Color.white, 0f);
        BoxLocal("Cork", stand, new Vector3(0f, 0f, 0.03f), new Vector3(1.42f, 1.62f, 0.04f), cork);
        BoxLocal("Frame_T", stand, new Vector3(0f, 0.83f, 0.03f), new Vector3(1.52f, 0.06f, 0.07f), wood);
        BoxLocal("Frame_B", stand, new Vector3(0f, -0.83f, 0.03f), new Vector3(1.52f, 0.06f, 0.07f), wood);
        BoxLocal("Frame_L", stand, new Vector3(-0.74f, 0f, 0.03f), new Vector3(0.06f, 1.72f, 0.07f), wood);
        BoxLocal("Frame_R", stand, new Vector3(0.74f, 0f, 0.03f), new Vector3(0.06f, 1.72f, 0.07f), wood);
        BoxLocal("Leg_L", stand, new Vector3(-0.6f, -1.13f, 0.08f), new Vector3(0.05f, 0.6f, 0.05f), metal);
        BoxLocal("Leg_R", stand, new Vector3(0.6f, -1.13f, 0.08f), new Vector3(0.05f, 0.6f, 0.05f), metal);
        BoxLocal("Foot", stand, new Vector3(0f, -1.4f, 0.08f), new Vector3(1.4f, 0.04f, 0.4f), metal);

        Canvas canvas = WorldCanvas("BoardCanvas", stand, boardPos + rot * new Vector3(0f, 0f, 0.005f), rot, new Vector2(1000f, 1150f), 0.00138f, interactive: true);
        Transform c = canvas.transform;

        // Pinned title card, always visible: it is part of the room, not of a station
        Image card = Img(c, "TitleCard", Paper);
        At(card.rectTransform, 70f, 40f, 860f, 190f);
        card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 1.2f);
        Img(card.transform, "Pin", new Color(0.8f, 0.15f, 0.12f), roundSprite).rectTransform.Let(r => At(r, 418f, 8f, 24f, 24f));
        TMP_Text title = Txt(card.transform, "Title", "SUSPECT UNKNOWN", 84f, new Color(0.1f, 0.1f, 0.12f), TextAlignmentOptions.Center);
        At(title.rectTransform, 0f, 28f, 860f, 96f);
        title.characterSpacing = 4f;
        TMP_Text sub = Txt(card.transform, "Subtitle", "DAY 4  |  SPEND IT BEFORE NIGHT FALLS", 28f, new Color(0.35f, 0.33f, 0.3f), TextAlignmentOptions.Center);
        At(sub.rectTransform, 0f, 124f, 860f, 40f);

        // Main buttons
        mainGroup = Group(c, "MainButtons");
        float y = 280f;
        Wire(MenuButton(mainGroup.transform, "Play", 70f, y, 860f, 140f, icons["play"], "PLAY", "Heist plan: pick tonight's target", 52f).fx, menu.OpenMap);
        Wire(MenuButton(mainGroup.transform, "Wardrobe", 70f, y += 158f, 860f, 140f, icons["shirt"], "WARDROBE", "Customization and burned items", 52f).fx, menu.OpenWardrobe);
        Wire(MenuButton(mainGroup.transform, "Shop", 70f, y += 158f, 860f, 140f, icons["bag"], "SHOP", "Skills, abilities, gear and the fence", 52f).fx, menu.OpenShop);
        Wire(MenuButton(mainGroup.transform, "Settings", 70f, y += 158f, 420f, 110f, icons["gear"], "SETTINGS", null, 40f).fx, menu.OpenSettings);
        Wire(MenuButton(mainGroup.transform, "Quit", 510f, y, 420f, 110f, icons["quit"], "QUIT", null, 40f).fx, menu.Quit);

        // Settings, in the same place on the board
        settingsGroup = Group(c, "Settings");
        Image plate = Img(settingsGroup.transform, "Plate", Ink);
        At(plate.rectTransform, 70f, 280f, 860f, 560f);
        Img(plate.transform, "Border", new Color(1f, 1f, 1f, 0.28f), borderSprite).rectTransform.Let(r => Stretch(r, 0f, 0f, 0f, 0f));
        TMP_Text head = Txt(plate.transform, "Heading", "SETTINGS", 48f, Color.white, TextAlignmentOptions.Left);
        At(head.rectTransform, 40f, 28f, 600f, 64f);
        Stepper(plate.transform, "Master volume", SettingsStepper.Setting.MasterVolume, 130f);
        Stepper(plate.transform, "Fullscreen", SettingsStepper.Setting.Fullscreen, 230f);
        Stepper(plate.transform, "Quality", SettingsStepper.Setting.Quality, 330f);
        Wire(MenuButton(settingsGroup.transform, "Back", 70f, 870f, 420f, 100f, icons["back"], "BACK", null, 40f).fx, menu.Back);
    }

    private static void Stepper(Transform parent, string label, SettingsStepper.Setting setting, float y)
    {
        RectTransform row = At(UI(label, parent), 40f, y, 780f, 80f);
        TMP_Text name = Txt(row, "Label", label.ToUpperInvariant(), 32f, Muted, TextAlignmentOptions.Left);
        At(name.rectTransform, 0f, 0f, 380f, 80f);
        Button prev = SmallButton(row, "<", 400f, 8f, 64f, 64f);
        TMP_Text value = Txt(row, "Value", "-", 32f, Color.white, TextAlignmentOptions.Center);
        At(value.rectTransform, 470f, 0f, 236f, 80f);
        Button next = SmallButton(row, ">", 712f, 8f, 64f, 64f);
        SettingsStepper stepper = row.gameObject.AddComponent<SettingsStepper>();
        Set(stepper, "setting", setting);
        Set(stepper, "previous", prev);
        Set(stepper, "next", next);
        Set(stepper, "valueLabel", value);
    }

    // ---------------------------------------------------------------- wardrobe

    private static void BuildWardrobeUi(PlayerCustomization player, out CanvasGroup left, out CanvasGroup right)
    {
        var host = new GameObject("WardrobeConcept");
        WardrobeConcept wardrobe = host.AddComponent<WardrobeConcept>();

        Vector3 leftPos = WardrobeSpot + new Vector3(-1.08f, 1.12f, -0.75f);
        Vector3 rightPos = WardrobeSpot + new Vector3(1.08f, 1.12f, -0.75f);
        Canvas lc = WorldCanvas("WardrobeCanvas", host.transform, leftPos, FaceFrom(leftPos, WardrobeCamPos), new Vector2(700f, 1180f), 0.00105f, interactive: true);
        Canvas rc = WorldCanvas("ProfileCanvas", host.transform, rightPos, FaceFrom(rightPos, WardrobeCamPos), new Vector2(700f, 1180f), 0.00105f, interactive: true);
        left = lc.gameObject.AddComponent<CanvasGroup>();
        right = rc.gameObject.AddComponent<CanvasGroup>();

        // Frosted backdrop so white text reads against the sunlit wall
        foreach (Canvas wc in new[] { lc, rc })
            Img(wc.transform, "Backdrop", new Color(0.05f, 0.06f, 0.07f, 0.55f)).rectTransform.Let(r => Stretch(r, -30f, -30f, -30f, -30f));

        Header(lc.transform, "WARDROBE", "Clothing is evidence. Dress for the witnesses.");
        Header(rc.transform, "POLICE PROFILE", "Suspect on file. Evidence piles up per job.");

        // One "< part >" picker per PlayerCustomization category, read from the prefab so new
        // categories show up on the next rebuild
        PlayerCustomization.CustomizationCategory[] categories = player.categories;
        var rows = new WardrobeConcept.Row[categories.Length];
        for (int i = 0; i < categories.Length; i++)
        {
            PlayerCustomization.CustomizationCategory category = categories[i];
            float y = 140f + i * 172f;
            TMP_Text title = Txt(lc.transform, category.categoryName, category.categoryName.ToUpperInvariant(), 26f, Amber, TextAlignmentOptions.Left);
            At(title.rectTransform, 0f, y, 700f, 34f);
            Image plate = Img(lc.transform, category.categoryName + "_Picker", Ink);
            At(plate.rectTransform, 0f, y + 38f, 700f, 110f);
            Img(plate.transform, "Border", new Color(1f, 1f, 1f, 0.28f), borderSprite).rectTransform.Let(r => Stretch(r, 0f, 0f, 0f, 0f));
            Button previous = ArrowButton(plate.transform, 5f, flip: false);
            Button next = ArrowButton(plate.transform, 595f, flip: true);
            TMP_Text part = Txt(plate.transform, "Part", "-", 36f, Color.white, TextAlignmentOptions.Center);
            At(part.rectTransform, 110f, 14f, 480f, 50f);
            TMP_Text count = Txt(plate.transform, "Count", "-", 22f, Muted, TextAlignmentOptions.Center);
            At(count.rectTransform, 110f, 66f, 480f, 30f);

            ProfileRow(rc.transform, category.categoryName, 140f + i * 150f, out TMP_Text label, out RectTransform bar, out TMP_Text status);
            ProfileFor(category, i, out string profilePart, out float confidence);
            rows[i] = new WardrobeConcept.Row
            {
                category = category.categoryName, previous = previous, next = next, partLabel = part, countLabel = count,
                profilePart = profilePart, profileConfidence = confidence, profileLabel = label, profileBar = bar, profileStatus = status
            };
        }

        TMP_Text hint = Txt(lc.transform, "Hint", "Drag the player to turn them around. Changes save instantly.", 22f, Muted, TextAlignmentOptions.Left);
        At(hint.rectTransform, 0f, 140f + categories.Length * 172f + 6f, 700f, 30f);
        Wire(MenuButton(lc.transform, "Back", 0f, 1060f, 340f, 96f, icons["back"], "BACK", null, 36f).fx, menu.Back);

        float below = 140f + categories.Length * 150f + 10f;
        TMP_Text warning = Txt(rc.transform, "Warning", "-", 26f, Color.white, TextAlignmentOptions.TopLeft);
        At(warning.rectTransform, 0f, below, 700f, 80f);
        warning.textWrappingMode = TextWrappingModes.Normal;
        Image reset = Img(rc.transform, "IdentityReset", Ink);
        At(reset.rectTransform, 0f, below + 100f, 700f, 70f);
        TMP_Text resetText = Txt(reset.transform, "Text", "IDENTITY RESET  <color=#F2B33D>$12,500</color>  <size=70%><color=#9A9A9A>(shop, safehouse)</color></size>", 28f, Color.white, TextAlignmentOptions.Center);
        Stretch(resetText.rectTransform, 0f, 0f, 0f, 0f);

        Set(wardrobe, "customization", player);
        Set(wardrobe, "rows", rows);
        Set(wardrobe, "warning", warning);
    }

    // Mock police profile built from the prefab's real part names: the first head is
    // confirmed (so the default look starts out burned), a red chest is the confirmed decoy,
    // the first legs are corroborated, and nothing is known about the feet
    private static void ProfileFor(PlayerCustomization.CustomizationCategory category, int index, out string part, out float confidence)
    {
        part = "";
        confidence = 0f;
        if (category.parts.Length == 0) return;
        switch (index)
        {
            case 0: part = category.parts[0].name; confidence = 0.82f; break;
            case 1:
                part = category.parts[Mathf.Min(1, category.parts.Length - 1)].name;
                foreach (GameObject p in category.parts)
                    if (p != null && p.name.EndsWith("_Red")) part = p.name;
                confidence = 0.93f;
                break;
            case 2: part = category.parts[0].name; confidence = 0.55f; break;
        }
    }

    private static void ProfileRow(Transform parent, string category, float y, out TMP_Text label, out RectTransform bar, out TMP_Text status)
    {
        Image plate = Img(parent, "Profile_" + category, Ink);
        At(plate.rectTransform, 0f, y, 700f, 132f);
        Img(plate.transform, "Border", new Color(1f, 1f, 1f, 0.2f), borderSprite).rectTransform.Let(r => Stretch(r, 0f, 0f, 0f, 0f));
        Image unknown = Img(plate.transform, "Silhouette", new Color(1f, 1f, 1f, 0.08f));
        At(unknown.rectTransform, 18f, 20f, 92f, 92f);
        TMP_Text q = Txt(unknown.transform, "Q", "?", 60f, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.Center);
        Stretch(q.rectTransform, 0f, 0f, 0f, 0f);
        label = Txt(plate.transform, "Part", "-", 30f, Color.white, TextAlignmentOptions.Left);
        At(label.rectTransform, 130f, 16f, 550f, 40f);
        status = Txt(plate.transform, "Status", "-", 24f, Muted, TextAlignmentOptions.Left);
        At(status.rectTransform, 130f, 56f, 550f, 32f);
        Image barBg = Img(plate.transform, "BarBg", new Color(1f, 1f, 1f, 0.1f));
        At(barBg.rectTransform, 130f, 98f, 546f, 10f);
        Image fill = Img(barBg.transform, "Fill", new Color(0.45f, 0.65f, 0.95f));
        Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);
        bar = fill.rectTransform;
    }

    private static Button ArrowButton(Transform parent, float x, bool flip)
    {
        RectTransform root = At(UI(flip ? "Next" : "Previous", parent), x, 5f, 100f, 100f);
        Image fill = root.gameObject.AddComponent<Image>();
        fill.color = Color.white;
        Button b = root.gameObject.AddComponent<Button>();
        ColorBlock colors = b.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.08f);
        colors.highlightedColor = new Color(0.95f, 0.7f, 0.24f, 0.9f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.6f);
        colors.selectedColor = colors.normalColor;
        colors.fadeDuration = 0.08f;
        b.colors = colors;
        Image arrow = Img(root, "Arrow", Color.white, icons["back"]);
        arrow.preserveAspect = true;
        RectTransform ar = arrow.rectTransform;
        ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0.5f);
        ar.sizeDelta = new Vector2(48f, 48f);
        if (flip) ar.localScale = new Vector3(-1f, 1f, 1f);
        return b;
    }

    // ---------------------------------------------------------------- shop

    private static CanvasGroup BuildShopUi(Wallet wallet)
    {
        var host = new GameObject("ShopConcept");
        ShopConcept shop = host.AddComponent<ShopConcept>();
        Quaternion rot = Quaternion.LookRotation(Vector3.forward);
        Canvas canvas = WorldCanvas("MonitorCanvas", host.transform, MonitorPos + new Vector3(0f, 0f, 0.015f), rot, new Vector2(1600f, 980f), 0.0011f, interactive: true);
        CanvasGroup group = canvas.gameObject.AddComponent<CanvasGroup>();
        Transform c = canvas.transform;
        Img(c, "Screen", new Color(0.05f, 0.06f, 0.08f, 1f)).rectTransform.Let(r => Stretch(r, 0f, 0f, 0f, 0f));

        TMP_Text site = Txt(c, "Site", "FENCE<color=#F2B33D>.NET</color>", 54f, Color.white, TextAlignmentOptions.Left);
        At(site.rectTransform, 50f, 30f, 600f, 70f);
        TMP_Text tag = Txt(c, "Tagline", "No questions asked. Mostly.", 24f, Muted, TextAlignmentOptions.Left);
        At(tag.rectTransform, 52f, 96f, 600f, 34f);

        // Wallet and the fence
        TMP_Text dirty = WalletStat(c, "DIRTY", 640f, new Color(0.62f, 0.85f, 0.5f));
        TMP_Text clean = WalletStat(c, "CLEAN", 860f, Color.white);
        TMP_Text points = WalletStat(c, "SKILL PTS", 1060f, Amber);
        ButtonParts launder = MenuButton(c, "Launder", 1240f, 34f, 310f, 92f, null, "LAUNDER", "fence", 32f);
        UnityEventTools.AddPersistentListener(launder.fx.GetComponent<Button>().onClick, shop.Launder);
        WalletView view = c.gameObject.AddComponent<WalletView>();
        Set(view, "wallet", wallet);
        Set(view, "dirty", dirty);
        Set(view, "clean", clean);
        Set(view, "skillPoints", points);
        Set(view, "fence", launder.subtitle);

        string[] tabNames = { "SKILLS", "ABILITIES", "GEAR", "SAFEHOUSE" };
        var tabs = new MenuButtonFx[tabNames.Length];
        var pages = new GameObject[tabNames.Length];
        for (int i = 0; i < tabNames.Length; i++)
        {
            tabs[i] = MenuButton(c, "Tab_" + tabNames[i], 50f + i * 284f, 160f, 270f, 70f, null, tabNames[i], null, 30f).fx;
            pages[i] = At(UI("Page_" + tabNames[i], c), 50f, 250f, 1500f, 560f).gameObject;
        }

        // Skills: three short lines (concept 7.4), each node needs the one above it
        string[,] skills =
        {
            { "GHOST", "Soft steps", "Less noise while moving", "Nimble fingers", "Faster lockpicking", "Shadow blend", "Slower suspicion in shadow", "Pack mule", "Full speed with a full bag" },
            { "FACE", "Blend in", "Standing still in public calms nearby suspicion", "Smooth talker", "Talk down a Curious NPC once per job", "Staff pass", "Pass as staff while in uniform", "Forgettable", "Witnesses observe you at reduced confidence" },
            { "HANDS", "Quick hands", "Faster looting", "Appraiser", "See values before taking", "Deep pockets", "Larger carry capacity", "Light touch", "Partial value from a display without obvious theft" },
        };
        for (int col = 0; col < 3; col++)
        {
            float x = col * 510f;
            TMP_Text h = Txt(pages[0].transform, "Line", skills[col, 0], 34f, Amber, TextAlignmentOptions.Left);
            At(h.rectTransform, x, 0f, 480f, 50f);
            ShopItem previous = null;
            for (int n = 0; n < 4; n++)
            {
                float y = 60f + n * 124f;
                if (n > 0) Img(pages[0].transform, "Link", new Color(1f, 1f, 1f, 0.25f)).rectTransform.Let(r => At(r, x + 40f, y - 24f, 4f, 24f));
                previous = Item(pages[0].transform, shop, wallet, x, y, 470f, 100f, skills[col, 1 + n * 2], skills[col, 2 + n * 2],
                    n < 2 ? 1 : 2, Wallet.Currency.SkillPoints, previous, owned: col == 0 && n == 0);
            }
        }

        // Abilities: device verbs (concept 6.7, decision 20)
        string[,] abilities =
        {
            { "Call the guard's mum", "Desk phone  |  2 uses per heist", "The guard walks to the phone and is stuck in the call, facing the wall. \"Yes mum... no mum...\"", "600" },
            { "Page the guard", "Store PA  |  2 uses per heist", "\"Security to the loading dock, your car is being towed.\" Sends the guard to the far end.", "750" },
            { "Leak the group chat", "Router  |  1 use per heist", "The guard stops and stares at his phone. His vision cone shrinks while he scrolls.", "900" },
            { "Clog the toilet", "Staff restroom  |  1 use per heist", "The clerk or manager runs over with a plunger and stays to fix it.", "500" },
            { "Car alarm", "Key fob  |  2 uses per heist", "Staff and shoppers turn to the window and face away from the shop floor.", "650" },
        };
        for (int i = 0; i < abilities.GetLength(0); i++)
            Item(pages[1].transform, shop, wallet, 0f, i * 108f, 1500f, 96f, abilities[i, 0], abilities[i, 2], int.Parse(abilities[i, 3]),
                Wallet.Currency.Clean, null, owned: i == 0, subtitle: abilities[i, 1]);

        // Gear (concept 7.3); lockpick levels gate locks (decision 19)
        ShopItem pick1 = Item(pages[2].transform, shop, wallet, 0f, 0f, 740f, 96f, "Lockpicks, level 1", "Opens level 1 locks. Picks never break.", 0, Wallet.Currency.Clean, null, owned: true, subtitle: "Silent, slow");
        ShopItem pick2 = Item(pages[2].transform, shop, wallet, 0f, 108f, 740f, 96f, "Lockpicks, level 2", "Opens level 2 locks: back offices, display cases.", 1200, Wallet.Currency.Clean, pick1, subtitle: "Needs level 1");
        Item(pages[2].transform, shop, wallet, 0f, 216f, 740f, 96f, "Lockpicks, level 3", "Opens level 3 locks: safes and vault gates.", 3000, Wallet.Currency.Clean, pick2, subtitle: "Needs level 2");
        Item(pages[2].transform, shop, wallet, 760f, 0f, 740f, 96f, "Glass cutter", "Silent entry through windows. Limited uses.", 1500, Wallet.Currency.Clean, null, subtitle: "Silent entry");
        Item(pages[2].transform, shop, wallet, 760f, 108f, 740f, 96f, "Camera looper", "Blinds one camera for a while. Someone at the monitor sees an empty aisle.", 2200, Wallet.Currency.Clean, null, subtitle: "Blinds one camera");
        Item(pages[2].transform, shop, wallet, 760f, 216f, 740f, 96f, "Signal jammer", "Delays the alarm call when things go wrong.", 2800, Wallet.Currency.Clean, null, subtitle: "Delays the alarm");
        Item(pages[2].transform, shop, wallet, 0f, 324f, 740f, 96f, "Bigger bag", "More carry capacity: more value per trip.", 900, Wallet.Currency.Clean, null, subtitle: "Carry capacity");

        // Safehouse upgrades (concept 7.5)
        ShopItem fence1 = Item(pages[3].transform, shop, wallet, 0f, 0f, 1500f, 96f, "Better fence contacts", "The fence pays 75% instead of 65% when laundering.", 2000, Wallet.Currency.Clean, null, subtitle: "Fence pays 75%", fenceRate: 0.75f);
        Item(pages[3].transform, shop, wallet, 0f, 108f, 1500f, 96f, "Trusted fence", "The fence pays 85% when laundering.", 5000, Wallet.Currency.Clean, fence1, subtitle: "Fence pays 85%", fenceRate: 0.85f);
        Item(pages[3].transform, shop, wallet, 0f, 216f, 1500f, 96f, "Intel: patrol routes", "Reveals where guards spend their time before the job.", 1000, Wallet.Currency.Clean, null, subtitle: "Pre-job intel");
        Item(pages[3].transform, shop, wallet, 0f, 324f, 1500f, 96f, "Wardrobe storage", "Keep more outfits, so burning one hurts less.", 1500, Wallet.Currency.Clean, null, subtitle: "More outfit slots");
        Item(pages[3].transform, shop, wallet, 0f, 432f, 1500f, 96f, "Identity reset", "Wipe the police profile. Expensive, and only once per campaign.", 12500, Wallet.Currency.Clean, null, subtitle: "Clears the suspect on file");

        // Detail line and back
        Image detail = Img(c, "Detail", new Color(1f, 1f, 1f, 0.05f));
        At(detail.rectTransform, 50f, 830f, 1180f, 110f);
        TMP_Text detailTitle = Txt(detail.transform, "Title", "", 30f, Amber, TextAlignmentOptions.Left);
        At(detailTitle.rectTransform, 24f, 12f, 1130f, 40f);
        TMP_Text detailBody = Txt(detail.transform, "Body", "", 26f, Color.white, TextAlignmentOptions.TopLeft);
        At(detailBody.rectTransform, 24f, 52f, 1130f, 56f);
        detailBody.textWrappingMode = TextWrappingModes.Normal;
        Wire(MenuButton(c, "Back", 1250f, 830f, 300f, 110f, icons["back"], "BACK", null, 36f).fx, menu.Back);

        Set(shop, "tabs", tabs);
        Set(shop, "pages", pages);
        Set(shop, "detailTitle", detailTitle);
        Set(shop, "detailBody", detailBody);
        Set(shop, "wallet", wallet);
        return group;
    }

    private static TMP_Text WalletStat(Transform parent, string label, float x, Color color)
    {
        TMP_Text l = Txt(parent, label, label, 22f, Muted, TextAlignmentOptions.Left);
        At(l.rectTransform, x, 40f, 200f, 30f);
        TMP_Text v = Txt(parent, label + "_Value", "-", 40f, color, TextAlignmentOptions.Left);
        At(v.rectTransform, x, 70f, 200f, 50f);
        return v;
    }

    private static ShopItem Item(Transform parent, ShopConcept shop, Wallet wallet, float x, float y, float w, float h, string title, string description,
        int price, Wallet.Currency currency, ShopItem requires, bool owned = false, string subtitle = null, float fenceRate = 0f)
    {
        RectTransform root = At(UI(title, parent), x, y, w, h);
        Image fill = root.gameObject.AddComponent<Image>();
        fill.color = Color.white;
        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        ColorBlock colors = button.colors;
        colors.normalColor = Ink;
        colors.highlightedColor = new Color(0.16f, 0.17f, 0.19f, 0.92f);
        colors.pressedColor = new Color(0.24f, 0.25f, 0.27f, 0.95f);
        colors.selectedColor = Ink;
        colors.disabledColor = new Color(0.07f, 0.08f, 0.09f, 0.6f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        Image border = Img(root, "Border", new Color(1f, 1f, 1f, 0.28f), borderSprite);
        Stretch(border.rectTransform, 0f, 0f, 0f, 0f);

        TMP_Text t = Txt(root, "Title", title.ToUpperInvariant(), subtitle == null ? 28f : 30f, Color.white, TextAlignmentOptions.Left);
        TMP_Text price_ = Txt(root, "Price", "-", 30f, Amber, TextAlignmentOptions.Right);
        At(price_.rectTransform, w - 220f, 0f, 196f, h);
        if (subtitle == null)
        {
            At(t.rectTransform, 22f, 8f, w - 40f, 44f);
            TMP_Text d = Txt(root, "Desc", description, 21f, Muted, TextAlignmentOptions.TopLeft);
            At(d.rectTransform, 22f, 50f, w - 240f, h - 52f);
            d.textWrappingMode = TextWrappingModes.Normal;
            price_.alignment = TextAlignmentOptions.TopRight;
            At(price_.rectTransform, w - 220f, 10f, 196f, 40f);
        }
        else
        {
            At(t.rectTransform, 22f, 10f, w - 260f, 44f);
            TMP_Text s = Txt(root, "Subtitle", subtitle, 22f, Muted, TextAlignmentOptions.Left);
            At(s.rectTransform, 22f, 52f, w - 260f, 34f);
        }

        ShopItem item = root.gameObject.AddComponent<ShopItem>();
        Set(item, "title", title);
        Set(item, "description", description);
        Set(item, "price", price);
        Set(item, "currency", currency);
        Set(item, "requires", requires);
        Set(item, "owned", owned);
        Set(item, "fenceRate", fenceRate);
        Set(item, "wallet", wallet);
        Set(item, "shop", shop);
        Set(item, "priceLabel", price_);
        Set(item, "border", border);
        return item;
    }

    // ---------------------------------------------------------------- HUD and map card

    private static CanvasGroup BuildHud(Wallet wallet, List<MapLocation> locations, GameObject labels, out MapBoard board)
    {
        var go = new GameObject("HUD", typeof(RectTransform));
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        go.layer = 5;
        Transform c = go.transform;

        // Top bar, always visible
        TMP_Text brand = Txt(c, "Brand", "SUSPECT UNKNOWN <color=#FFFFFFAA><size=60%>- Safehouse</size></color>", 40f, Color.white, TextAlignmentOptions.Left);
        At(brand.rectTransform, 40f, 28f, 900f, 56f);
        Shadow(brand);
        RectTransform walletBox = UI("Wallet", c);
        walletBox.anchorMin = walletBox.anchorMax = walletBox.pivot = new Vector2(1f, 1f);
        walletBox.anchoredPosition = new Vector2(-40f, -24f);
        walletBox.sizeDelta = new Vector2(620f, 90f);
        TMP_Text name = Txt(walletBox, "Player", "PLAYER_NAME  |", 30f, Color.white, TextAlignmentOptions.Right);
        At(name.rectTransform, 0f, 4f, 330f, 44f);
        TMP_Text clean = Txt(walletBox, "Clean", "-", 40f, new Color(0.62f, 0.9f, 0.55f), TextAlignmentOptions.Left);
        At(clean.rectTransform, 344f, 0f, 276f, 50f);
        TMP_Text dirty = Txt(walletBox, "Dirty", "Dirty money {0}", 24f, Muted, TextAlignmentOptions.Right);
        At(dirty.rectTransform, 0f, 50f, 620f, 34f);
        foreach (TMP_Text t in new[] { name, clean, dirty }) Shadow(t);
        WalletView view = walletBox.gameObject.AddComponent<WalletView>();
        Set(view, "wallet", wallet);
        Set(view, "clean", clean);
        Set(view, "dirty", dirty);

        // Map station overlay
        CanvasGroup map = Group(c, "MapOverlay");
        TMP_Text heading = Txt(map.transform, "Heading", "PICK TONIGHT'S TARGET", 48f, Color.white, TextAlignmentOptions.Left);
        At(heading.rectTransform, 40f, 100f, 900f, 60f);
        Shadow(heading);
        TMP_Text hint = Txt(map.transform, "Hint", "Hover a building to scout it, click to read the briefing", 26f, Muted, TextAlignmentOptions.Left);
        At(hint.rectTransform, 42f, 158f, 900f, 36f);
        Shadow(hint);
        ButtonParts back = MenuButton(map.transform, "Back", 40f, 0f, 300f, 96f, icons["back"], "BACK", "To the safehouse", 34f);
        back.fx.GetComponent<RectTransform>().Let(r => { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 0f); r.anchoredPosition = new Vector2(40f, 40f); });
        Wire(back.fx, menu.Back);

        // Briefing card
        RectTransform cardRt = UI("BriefingCard", map.transform);
        cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(1f, 0.5f);
        cardRt.anchoredPosition = new Vector2(-40f, -20f);
        cardRt.sizeDelta = new Vector2(560f, 640f);
        CanvasGroup card = cardRt.gameObject.AddComponent<CanvasGroup>();
        Img(cardRt, "Plate", new Color(0.06f, 0.07f, 0.1f, 0.88f)).rectTransform.Let(r => Stretch(r, 0f, 0f, 0f, 0f));
        Img(cardRt, "Border", new Color(1f, 0.9f, 0.2f, 0.8f), borderSprite).rectTransform.Let(r => Stretch(r, 0f, 0f, 0f, 0f));
        TMP_Text title = Txt(cardRt, "Title", "-", 50f, Color.white, TextAlignmentOptions.Left);
        At(title.rectTransform, 36f, 30f, 500f, 64f);
        TMP_Text job = Txt(cardRt, "Job", "-", 26f, Muted, TextAlignmentOptions.Left);
        At(job.rectTransform, 36f, 94f, 500f, 36f);
        TMP_Text stars = CardField(cardRt, "DIFFICULTY", 150f);
        TMP_Text security = CardField(cardRt, "SECURITY", 230f);
        TMP_Text take = CardField(cardRt, "EXPECTED TAKE", 310f);
        TMP_Text note = CardField(cardRt, "POLICE PROFILE", 390f);
        note.textWrappingMode = TextWrappingModes.Normal;
        note.fontSize = 24f;
        note.rectTransform.sizeDelta = new Vector2(490f, 90f);

        var host = new GameObject("MapBoard");
        board = host.AddComponent<MapBoard>();
        ButtonParts plan = MenuButton(cardRt, "PlanHeist", 36f, 520f, 488f, 92f, icons["play"], "PLAN HEIST", "Night falls. Case the joint.", 34f);
        UnityEventTools.AddPersistentListener(plan.fx.GetComponent<Button>().onClick, board.PlanHeist);

        Set(board, "labels", labels);
        Set(board, "card", card);
        Set(board, "title", title);
        Set(board, "jobType", job);
        Set(board, "stars", stars);
        Set(board, "security", security);
        Set(board, "take", take);
        Set(board, "profileNote", note);
        return map;
    }

    private static TMP_Text CardField(Transform card, string label, float y)
    {
        TMP_Text l = Txt(card, label, label, 22f, Amber, TextAlignmentOptions.Left);
        At(l.rectTransform, 36f, y, 490f, 30f);
        TMP_Text v = Txt(card, label + "_Value", "-", 32f, Color.white, TextAlignmentOptions.TopLeft);
        At(v.rectTransform, 36f, y + 30f, 490f, 44f);
        return v;
    }

    // ---------------------------------------------------------------- UI building blocks

    private struct ButtonParts
    {
        public MenuButtonFx fx;
        public Image icon;
        public TMP_Text title, subtitle;
    }

    // The button style from the reference: dark plate, thin light border, white icon, big title, small subtitle
    private static ButtonParts MenuButton(Transform parent, string name, float x, float y, float w, float h, Sprite icon,
        string title, string subtitle, float titleSize, bool swatch = false)
    {
        RectTransform root = At(UI(name, parent), x, y, w, h);
        Image fill = root.gameObject.AddComponent<Image>();
        fill.color = Ink;
        Button button = root.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = fill;

        Image border = Img(root, "Border", new Color(1f, 1f, 1f, 0.28f), borderSprite);
        Stretch(border.rectTransform, 0f, 0f, 0f, 0f);
        Image accent = Img(root, "Accent", Amber);
        RectTransform ar = accent.rectTransform;
        ar.anchorMin = new Vector2(0f, 0f);
        ar.anchorMax = new Vector2(0f, 1f);
        ar.pivot = new Vector2(0f, 0.5f);
        ar.sizeDelta = new Vector2(0f, 0f);
        ar.anchoredPosition = Vector2.zero;

        RectTransform content = UI("Content", root);
        Stretch(content, 0f, 0f, 0f, 0f);
        var parts = new ButtonParts();
        float tx = 28f;
        if (icon != null || swatch)
        {
            float size = swatch ? h * 0.5f : Mathf.Min(h * 0.52f, 64f);
            parts.icon = Img(content, swatch ? "Swatch" : "Icon", Color.white, swatch ? null : icon);
            parts.icon.preserveAspect = true;
            At(parts.icon.rectTransform, 26f, (h - size) / 2f, size, size);
            tx = 26f + size + 24f;
        }
        parts.title = Txt(content, "Title", title, titleSize, Color.white, subtitle == null ? TextAlignmentOptions.Left : TextAlignmentOptions.BottomLeft);
        if (subtitle == null) At(parts.title.rectTransform, tx, 0f, w - tx - 12f, h);
        else
        {
            At(parts.title.rectTransform, tx, 0f, w - tx - 12f, h * 0.58f);
            parts.subtitle = Txt(content, "Subtitle", subtitle, Mathf.Round(titleSize * 0.52f), Muted, TextAlignmentOptions.TopLeft);
            At(parts.subtitle.rectTransform, tx, h * 0.58f, w - tx - 12f, h * 0.4f);
        }

        parts.fx = root.gameObject.AddComponent<MenuButtonFx>();
        Set(parts.fx, "fill", fill);
        Set(parts.fx, "border", border);
        Set(parts.fx, "accent", ar);
        Set(parts.fx, "content", content);
        return parts;
    }

    private static Button SmallButton(Transform parent, string glyph, float x, float y, float w, float h)
    {
        RectTransform root = At(UI("Button" + glyph, parent), x, y, w, h);
        Image fill = root.gameObject.AddComponent<Image>();
        fill.color = Color.white;
        Button b = root.gameObject.AddComponent<Button>();
        ColorBlock colors = b.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.1f);
        colors.highlightedColor = new Color(0.95f, 0.7f, 0.24f, 0.9f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.6f);
        colors.selectedColor = colors.normalColor;
        b.colors = colors;
        TMP_Text t = Txt(root, "Glyph", glyph, 36f, Color.white, TextAlignmentOptions.Center);
        Stretch(t.rectTransform, 0f, 0f, 0f, 0f);
        return b;
    }

    private static void Header(Transform parent, string title, string subtitle)
    {
        TMP_Text t = Txt(parent, "Header", title, 48f, Color.white, TextAlignmentOptions.Left);
        At(t.rectTransform, 0f, 0f, 700f, 64f);
        Shadow(t);
        TMP_Text s = Txt(parent, "SubHeader", subtitle, 24f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Left);
        At(s.rectTransform, 0f, 66f, 700f, 40f);
        Shadow(s);
        Img(parent, "Rule", Amber).rectTransform.Let(r => At(r, 0f, 112f, 120f, 4f));
    }

    private static Canvas WorldCanvas(string name, Transform parent, Vector3 pos, Quaternion rot, Vector2 size, float scale, bool interactive)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, true);
        go.layer = 5;
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = mainCam;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.SetPositionAndRotation(pos, rot);
        rt.localScale = Vector3.one * scale / (parent != null ? parent.lossyScale.x : 1f);
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
        if (interactive) go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static Quaternion FaceFrom(Vector3 pos, Vector3 viewer)
    {
        Vector3 d = pos - viewer;
        d.y = 0f;
        return Quaternion.LookRotation(d.normalized);
    }

    private static CanvasGroup Group(Transform parent, string name)
    {
        RectTransform rt = UI(name, parent);
        Stretch(rt, 0f, 0f, 0f, 0f);
        return rt.gameObject.AddComponent<CanvasGroup>();
    }

    private static RectTransform UI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // Top-left anchored placement in parent pixels, y going down, like a layout tool
    private static RectTransform At(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private static void Center(RectTransform rt, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rt, float l, float t, float r, float b)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    private static Image Img(Transform parent, string name, Color color, Sprite sprite = null)
    {
        Image img = UI(name, parent).gameObject.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text Txt(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
    {
        TextMeshProUGUI t = UI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    // Soft drop shadow so HUD text reads over a bright sky
    private static void Shadow(TMP_Text t)
    {
        t.fontSharedMaterial = ShadowMaterial();
    }

    private static Material ShadowMaterial()
    {
        string path = Root + "Materials/Electrolize_Shadow.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(font.material);
            AssetDatabase.CreateAsset(m, path);
        }
        m.EnableKeyword("UNDERLAY_ON");
        m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));
        m.SetFloat("_UnderlayOffsetX", 0.4f);
        m.SetFloat("_UnderlayOffsetY", -0.4f);
        m.SetFloat("_UnderlaySoftness", 0.5f);
        return m;
    }

    private static void Wire(MenuButtonFx fx, UnityAction action) =>
        UnityEventTools.AddPersistentListener(fx.GetComponent<Button>().onClick, action);

    private static T Let<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }

    // ---------------------------------------------------------------- geometry and assets

    private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3? euler = null)
    {
        GameObject go = Shape(PrimitiveType.Cube, name, null, pos, size, mat, euler).gameObject;
        go.transform.SetParent(parent, true);
        return go;
    }

    private static GameObject BoxLocal(string name, Transform parent, Vector3 local, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static void Cylinder(string name, Transform parent, Vector3 pos, Vector3 size, Material mat) =>
        Shape(PrimitiveType.Cylinder, name, parent, pos, size, mat);

    // Primitive without a collider: only map targets carry colliders, so the physics raycaster sees nothing else
    private static Renderer Shape(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3? euler = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
        go.transform.localScale = size;
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        return r;
    }

    private static GameObject Prefab(string path, Transform parent, Vector3 pos, float yaw, bool fitToLot = false, float scale = 1f)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(SyntyCity + path + ".prefab");
        if (asset == null)
        {
            Debug.LogWarning($"MainMenuConceptBuilder: prefab not found, skipped: {path}");
            return new GameObject("Missing_" + path);
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = Vector3.one * scale;
        foreach (Collider col in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);

        // Synty pivots sit on a corner; centre the footprint on the lot and stand it on the ground
        Bounds b = WorldBounds(go);
        Vector3 shift = new Vector3(pos.x - b.center.x, fitToLot ? pos.y - b.min.y : 0f, pos.z - b.center.z);
        if (fitToLot || path.StartsWith("Props")) go.transform.position += shift;
        return go;
    }

    private static Bounds WorldBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static Material Mat(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
    {
        string path = Root + "Materials/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null && color == Color.white && smoothness == 0f) return m; // lookup only
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material Unlit(string name, Color color)
    {
        string path = Root + "Materials/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Private serialized fields are set through reflection: the components keep their
    // fields private and the builder does not need a SerializedProperty path per nested field
    private static void Set(Object target, string field, object value)
    {
        FieldInfo info = null;
        for (Type t = target.GetType(); t != null && info == null; t = t.BaseType)
            info = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (info == null)
        {
            Debug.LogError($"MainMenuConceptBuilder: {target.GetType().Name} has no field '{field}'");
            return;
        }
        info.SetValue(target, value);
        EditorUtility.SetDirty(target);
    }

    // ---------------------------------------------------------------- generated sprites

    private static void BuildSprites()
    {
        Directory.CreateDirectory(Root + "Materials");
        Directory.CreateDirectory(Root + "Sprites");

        borderSprite = SaveSprite("Border", Draw(32, (x, y) => x < 2 || y < 2 || x > 29 || y > 29 ? 1f : 0f), new Vector4(4, 4, 4, 4));
        roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        icons["play"] = SaveSprite("Icon_Play", Icon(p => Poly(p, new Vector2(42, 26), new Vector2(42, 102), new Vector2(104, 64)) - 3f), Vector4.zero);
        icons["shirt"] = SaveSprite("Icon_Shirt", Icon(p => Mathf.Max(
            Poly(p, new Vector2(46, 20), new Vector2(56, 26), new Vector2(72, 26), new Vector2(82, 20), new Vector2(112, 42), new Vector2(100, 60),
                new Vector2(88, 52), new Vector2(88, 108), new Vector2(40, 108), new Vector2(40, 52), new Vector2(28, 60), new Vector2(16, 42)) - 2f,
            -Circle(p, new Vector2(64, 20), 11f))), Vector4.zero);
        icons["bag"] = SaveSprite("Icon_Bag", Icon(p => Mathf.Min(
            Box2(p, new Vector2(64, 78), new Vector2(34, 32)) - 3f,
            Mathf.Max(Mathf.Abs(Circle(p, new Vector2(64, 46), 18f)) - 5f, p.y - 46f))), Vector4.zero);
        icons["gear"] = SaveSprite("Icon_Gear", Icon(p =>
        {
            float d = Circle(p, new Vector2(64, 64), 30f);
            for (int k = 0; k < 8; k++)
            {
                Vector2 q = Rotate(p - new Vector2(64, 64), k * 45f) + new Vector2(64, 64);
                d = Mathf.Min(d, Box2(q, new Vector2(64, 28), new Vector2(9, 12)) - 1.5f);
            }
            return Mathf.Max(d, -Circle(p, new Vector2(64, 64), 13f));
        }), Vector4.zero);
        icons["quit"] = SaveSprite("Icon_Quit", Icon(p =>
        {
            float frame = Mathf.Max(Box2(p, new Vector2(48, 64), new Vector2(24, 42)), -Box2(p, new Vector2(48, 64), new Vector2(16, 34)));
            frame = Mathf.Max(frame, -Box2(p, new Vector2(70, 64), new Vector2(8, 15)));
            float arrow = Mathf.Min(Segment(p, new Vector2(56, 64), new Vector2(98, 64), 6f), Poly(p, new Vector2(92, 46), new Vector2(116, 64), new Vector2(92, 82)));
            return Mathf.Min(frame, arrow);
        }), Vector4.zero);
        icons["back"] = SaveSprite("Icon_Back", Icon(p => Mathf.Min(
            Segment(p, new Vector2(40, 64), new Vector2(104, 64), 7f),
            Poly(p, new Vector2(60, 34), new Vector2(22, 64), new Vector2(60, 94)))), Vector4.zero);
    }

    private static float[,] Icon(Func<Vector2, float> sdf) =>
        Draw(128, (x, y) => Mathf.Clamp01(0.5f - sdf(new Vector2(x + 0.5f, 127.5f - y))));

    private static float[,] Draw(int size, Func<int, int, float> alpha)
    {
        var a = new float[size, size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                a[x, y] = alpha(x, y);
        return a;
    }

    private static Sprite SaveSprite(string name, float[,] alpha, Vector4 border)
    {
        int size = alpha.GetLength(0);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha[x, y]));
        string path = Root + "Sprites/" + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Signed distance helpers for the icons, in 128 px icon space with y going down
    private static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

    private static float Box2(Vector2 p, Vector2 c, Vector2 half)
    {
        Vector2 d = new Vector2(Mathf.Abs(p.x - c.x) - half.x, Mathf.Abs(p.y - c.y) - half.y);
        return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
    }

    private static float Segment(Vector2 p, Vector2 a, Vector2 b, float thickness)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - thickness;
    }

    private static float Poly(Vector2 p, params Vector2[] v)
    {
        float d = Vector2.Dot(p - v[0], p - v[0]);
        float s = 1f;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
        {
            Vector2 e = v[j] - v[i], w = p - v[i];
            Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
            d = Mathf.Min(d, Vector2.Dot(b, b));
            bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
        }
        return s * Mathf.Sqrt(d);
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }
}
