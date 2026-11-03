using System.Collections.Generic;
using UnityEngine;

// Shows the floor the player is on and fades the others, so a player on one floor never learns what
// another floor holds (decision 26). One per level. Everything runs off one continuous view level:
// 1 on floor 1, 1.5 halfway up the stairs to floor 2. FloorVolumes say which floor the player is on;
// on FloorStairs the player's height drives the level instead, so the floors fade with each step;
// anywhere else a change of floor (a door, the garage) fades over fadeDuration.
//  - Building: a floor above the player fades to its belowHide, the part over the floor below fully.
//    The tops of the player's own floor's walls fade out too, so the camera sees into the rooms.
//    While the player is outside, the whole building shows.
//  - Props (loot, doors, lamps) and FloorHideables (NPCs) fade out fully whenever their floor is not
//    the player's, so nothing on another floor gives itself away.
// Fading uses the OccluderWall shader's dither through RendererFade. Core, so NPC code can read it
public class BuildingFloors : MonoBehaviour
{
    // Off value for forcedFloor; -1 is already taken by "outside"
    private const int NotForced = -2;

    // Metres something may sit below a floor's height and still count as on it (a step, the NavMesh offset)
    private const float FloorTolerance = 0.5f;

    [Header("Floors")]
    [SerializeField, Tooltip("The building's floors, bottom to top. The roof can be last")]
    private BuildingFloor[] floors;

    [SerializeField, Tooltip("Roots of props that stay put: loot, doors, lamps. Each renderer fades out fully while the player is on another floor")]
    private Transform[] propRoots;

    [SerializeField, Tooltip("The player's root. Its height places the player on the stairs and outside")]
    private Transform player;

    [Header("Fade")]
    [SerializeField, Min(0.01f), Tooltip("Seconds for one floor of fade where no stairs drive it: doors, the garage")]
    private float fadeDuration = 0.5f;

    [Header("Debug")]
    [SerializeField, Tooltip("Log every change of the player's floor to the console")]
    private bool logChanges = true;

    [SerializeField, Tooltip("Play mode only: view this floor whatever the volumes say. -1 is outside, -2 is off")]
    private int forcedFloor = NotForced;

    public static BuildingFloors Instance { get; private set; }

    // From the volumes: -1 while the player is outside
    public int PlayerFloor { get; private set; } = -1;

    // Building renderers, one entry each. Parallel lists, so nothing allocates while fading
    private readonly List<RendererFade> shellFades = new();
    private readonly List<int> shellFloors = new();
    private readonly List<float> shellBelowHides = new();
    private readonly List<bool> shellIsTop = new();

    private readonly List<RendererFade> propFades = new();
    private readonly List<int> propFloors = new();

    private readonly List<FloorHideable> hideables = new();

    // Counted per floor, not a bool: a floor can have several boxes, and walking between them sends
    // Enter on the next box before Exit on the last
    private int[] insideCounts;

    // Occupied floors in the order the player entered them. The newest wins, and stepping back
    // returns to the floor the player never left
    private readonly List<int> entered = new();

    private int stairsCount;
    private int stairsLowerFloor;

    // The displayed state, eased toward the target each frame. inside is 0 outside, 1 in the building
    private float level;
    private float inside;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{name} is a second BuildingFloors in the scene; only {Instance.name} is used", this);
            enabled = false;
            return;
        }

        if (floors == null || floors.Length == 0 || player == null)
        {
            Debug.LogWarning($"{name} needs its floors and the player; nothing will fade", this);
            enabled = false;
            return;
        }

        Instance = this;
        insideCounts = new int[floors.Length];

        for (int i = 0; i < floors.Length; i++)
            CollectShell(i);

        if (propRoots != null)
            foreach (Transform propRoot in propRoots)
                if (propRoot != null) CollectProps(propRoot);
    }

    // After every Awake, so the player has its spawn position: start in the right state, unfaded
    private void Start()
    {
        level = TargetLevel(out inside);
        Apply();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnValidate()
    {
        if (forcedFloor < NotForced) forcedFloor = NotForced;
    }

    // Continuous smoothing: idle unless the target moved, which on the stairs is every step
    private void Update()
    {
        float target = TargetLevel(out float insideTarget);
        if (level == target && inside == insideTarget) return;

        float step = Time.deltaTime / fadeDuration;
        level = Mathf.MoveTowards(level, target, step);
        inside = Mathf.MoveTowards(inside, insideTarget, step);
        Apply();
    }

    public int FloorAt(float y)
    {
        for (int i = floors.Length - 1; i > 0; i--)
            if (y >= floors[i].height - FloorTolerance) return i;
        return 0;
    }

    // How far something standing on floor is faded: 0 when the player views that floor, 1 a floor away
    public float HideOnFloor(int floor) => inside * Mathf.Clamp01(Mathf.Abs(level - floor));

    public void Register(FloorHideable hideable)
    {
        if (!hideables.Contains(hideable)) hideables.Add(hideable);
    }

    public void Unregister(FloorHideable hideable) => hideables.Remove(hideable);

    // Called by FloorVolume once per collider the player enters
    public void Enter(int floor)
    {
        if (!enabled || !IsFloor(floor)) return;
        if (insideCounts[floor]++ == 0) entered.Add(floor);
        SetPlayerFloor(entered[entered.Count - 1]);
    }

    // Called by FloorVolume once per collider the player leaves
    public void Exit(int floor)
    {
        if (!enabled || !IsFloor(floor) || insideCounts[floor] == 0) return;
        if (--insideCounts[floor] == 0) entered.Remove(floor);
        SetPlayerFloor(entered.Count > 0 ? entered[entered.Count - 1] : -1);
    }

    // Called by FloorStairs once per collider the player enters
    public void EnterStairs(int lowerFloor)
    {
        if (!enabled) return;
        if (lowerFloor < 0 || lowerFloor >= floors.Length - 1)
        {
            Debug.LogWarning($"{name} has no floor above {lowerFloor}; check the FloorStairs", this);
            return;
        }

        stairsLowerFloor = lowerFloor;
        stairsCount++;
    }

    // Called by FloorStairs once per collider the player leaves
    public void ExitStairs()
    {
        if (enabled && stairsCount > 0) stairsCount--;
    }

    private bool IsFloor(int floor)
    {
        if (floor >= 0 && floor < floors.Length) return true;
        Debug.LogWarning($"{name} has no floor {floor}; check the FloorVolume", this);
        return false;
    }

    private void SetPlayerFloor(int floor)
    {
        if (floor == PlayerFloor) return;
        if (logChanges) Debug.Log($"{name} floor {PlayerFloor} -> {floor}", this);
        PlayerFloor = floor;
    }

    private float TargetLevel(out float insideTarget)
    {
        if (forcedFloor == NotForced && stairsCount > 0)
        {
            insideTarget = 1f;
            return Mathf.Clamp(LevelAt(player.position.y), stairsLowerFloor, stairsLowerFloor + 1);
        }

        int floor = forcedFloor != NotForced ? forcedFloor : PlayerFloor;
        if (floor >= 0)
        {
            insideTarget = 1f;
            return Mathf.Min(floor, floors.Length - 1);
        }

        // Outside: the building shows whole, and NPCs and props go by the floor at the player's height,
        // so the street sees the ground floor's and the bottom of the garage ramp the lower floor's
        insideTarget = 0f;
        return FloorAt(player.position.y);
    }

    // A height on the stairs as a continuous level between the floors' heights
    private float LevelAt(float y)
    {
        if (y <= floors[0].height) return 0f;
        for (int i = 0; i < floors.Length - 1; i++)
            if (y < floors[i + 1].height)
                return i + Mathf.InverseLerp(floors[i].height, floors[i + 1].height, y);
        return floors.Length - 1;
    }

    private void Apply()
    {
        for (int i = 0; i < shellFades.Count; i++)
        {
            int floor = shellFloors[i];
            float below = shellBelowHides[i] * Mathf.Clamp01(floor - level);
            float top = shellIsTop[i] ? Mathf.Clamp01(1f - Mathf.Abs(level - floor)) : 0f;
            shellFades[i].Set(inside * Mathf.Max(below, top));
        }

        for (int i = 0; i < propFades.Count; i++)
            propFades[i].Set(HideOnFloor(propFloors[i]));

        foreach (FloorHideable hideable in hideables)
            hideable.Refresh();
    }

    private void CollectShell(int floor)
    {
        BuildingFloor data = floors[floor];
        if (data.root == null) return;

        var tops = new HashSet<Renderer>();
        if (data.wallTops != null) tops.UnionWith(data.wallTops.GetComponentsInChildren<Renderer>(true));

        bool hasArea = data.fullHideArea.size != Vector3.zero;
        foreach (Renderer r in data.root.GetComponentsInChildren<Renderer>(true))
        {
            var fade = new RendererFade(r, true);
            if (!fade.CanFade) continue;

            shellFades.Add(fade);
            shellFloors.Add(floor);
            shellBelowHides.Add(hasArea && data.fullHideArea.Contains(r.bounds.center) ? 1f : data.belowHide);
            shellIsTop.Add(tops.Contains(r));
        }
    }

    // The transform's height, not the renderer's bounds: props hidden at load (unrolled loot) have no bounds
    private void CollectProps(Transform propRoot)
    {
        foreach (Renderer r in propRoot.GetComponentsInChildren<Renderer>(true))
        {
            var fade = new RendererFade(r, false);
            if (!fade.CanFade) continue;

            propFades.Add(fade);
            propFloors.Add(FloorAt(r.transform.position.y));
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (floors == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.5f);
        foreach (BuildingFloor floor in floors)
            if (floor.fullHideArea.size != Vector3.zero)
                Gizmos.DrawWireCube(floor.fullHideArea.center, floor.fullHideArea.size);
    }
}
