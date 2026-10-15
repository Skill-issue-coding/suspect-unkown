using System;
using System.Collections;
using PrimeTween;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Concept main menu: the safehouse in daylight. Each menu entry is a station in the room;
// switching station activates that station's Cinemachine camera (the brain blends the pan)
// and fades its world-space panels in once the camera is nearly there. Where a straight blend
// would pass through furniture, a CameraRoute carries a travel camera along a spline instead.
// Play is the one exception: the camera dives into the planning table while the room
// fades from day to night, because the heist itself always happens at night.
public class MainMenuConcept : MonoBehaviour
{
    [Serializable]
    public struct StationView
    {
        [Tooltip("Station this entry describes")]
        public MenuStation station;
        [Tooltip("Camera framing the station. Stations may share a camera")]
        public CinemachineCamera camera;
        [Tooltip("Panels shown only while this station is active")]
        public CanvasGroup[] panels;
        [Tooltip("Metres from the camera where the background blur starts")]
        public float focusStart;
        [Tooltip("Metres from the camera where the background blur is full")]
        public float focusEnd;
    }

    [Header("Stations")]
    [Tooltip("One entry per station")]
    [SerializeField] private StationView[] stations;
    [Tooltip("Station shown when the scene starts")]
    [SerializeField] private MenuStation startStation = MenuStation.Main;
    [SerializeField] private Volume postProcessingVolume;


    [Header("Transitions")]
    [Tooltip("Seconds to wait after a camera switch before panels fade in, roughly the brain's blend time")]
    [SerializeField] private float panelDelay = 0.9f;
    [Tooltip("Seconds a panel takes to fade in or out")]
    [SerializeField] private float panelFade = 0.3f;
    [Tooltip("Blends the lighting between day and night for the map station")]
    [SerializeField] private DayNightBlend dayNight;
    [Tooltip("Map board, switched on only while the map station is active")]
    [SerializeField] private MapBoard mapBoard;

    [Header("Routes")]
    [Tooltip("Paths between station pairs whose straight blend clips through something. Any other pair blends straight")]
    [SerializeField] private CameraRoute[] routes;
    [Tooltip("Camera moved along a route. Inactive in the scene; only live while a route plays")]
    [SerializeField] private CinemachineCamera travelCamera;

    private MenuStation current;
    private Coroutine panelRoutine;
    private InputAction backAction;

    private DepthOfField depthOfField;
    private float fromStart, fromEnd, toStart, toEnd;
    private Tween focusTween;

    private CameraRoute route;
    private bool routeReversed, routeArrived;
    private Vector3 routeFrom, routeTo;
    private Quaternion routeFromRotation, routeToRotation;
    private float routeFromFov;
    private CinemachineCamera routeDestination;
    private Tween routeTween;

    public MenuStation Current => current;

    // Raised on every station switch, after the camera starts moving, with the delay until the
    // station's panels have faded in. Lets a station play its own intro once it is visible
    public event Action<MenuStation, float> StationShown;

    private void OnValidate()
    {
        if (stations == null) return;
        foreach (StationView stationView in stations)
            if (stationView.focusEnd <= stationView.focusStart)
                Debug.LogWarning($"{name}: station {stationView.station} needs focusEnd greater than focusStart", this);

    }

    private void Awake()
    {
        if (stations == null || stations.Length == 0)
        {
            Debug.LogWarning($"{name} has no stations assigned", this);
            enabled = false;
            return;
        }

        if (postProcessingVolume == null || !postProcessingVolume.profile.TryGet(out depthOfField))
            Debug.LogWarning($"{name} has no Depth Of Field on its volume; focus stays fixed", this);
        else
            depthOfField.mode.Override(DepthOfFieldMode.Gaussian);

        // Escape or right mouse steps back, so every station can be left without the mouse
        backAction = new InputAction("Back", InputActionType.Button);
        backAction.AddBinding("<Keyboard>/escape");
        backAction.AddBinding("<Mouse>/rightButton");
    }

    private void Start()
    {
        foreach (StationView view in stations)
            foreach (CanvasGroup panel in view.panels)
                SetPanel(panel, 0f);

        // A completed job leaves its debrief pending: the menu opens on it instead of the start station
        MenuStation first = ProgressStore.Load().debriefPending ? MenuStation.Debrief : startStation;
        Show(first, instant: true);
    }

    private void OnEnable()
    {
        if (backAction == null) return;
        backAction.performed += OnBack;
        backAction.Enable();
        CinemachineCore.GetBlendOverride += OverrideBlend;
    }

    private void OnDisable()
    {
        if (backAction == null) return;
        backAction.performed -= OnBack;
        backAction.Disable();
        CinemachineCore.GetBlendOverride -= OverrideBlend;
    }

    // Button targets. Parameterless so the scene builder can wire them as persistent listeners.
    public void OpenMain() => Show(MenuStation.Main);
    public void OpenSettings() => Show(MenuStation.Settings);
    public void OpenWardrobe() => Show(MenuStation.Wardrobe);
    public void OpenShop() => Show(MenuStation.Shop);
    public void OpenMap() => Show(MenuStation.Map);
    public void OpenDebrief() => Show(MenuStation.Debrief);

    public void Back()
    {
        if (current != MenuStation.Main) Show(MenuStation.Main);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnBack(InputAction.CallbackContext context) => Back();

    private void Show(MenuStation station, bool instant = false)
    {
        if (!TryGetView(station, out StationView target))
        {
            Debug.LogWarning($"{name} has no view for station {station}", this);
            return;
        }

        bool cameraMoves = TryGetView(current, out StationView previous) && previous.camera != target.camera;
        MenuStation source = current;
        current = station;

        // A new switch mid-route blends on from wherever the travel camera got to: starting a route
        // here would cut back to the source camera's pose first
        bool wasTravelling = route != null;
        routeTween.Stop();
        route = null;

        float travelTime = panelDelay;
        if (!instant && !wasTravelling && cameraMoves && previous.camera != null && target.camera != null && TryGetRoute(source, station, out CameraRoute found, out bool reversed))
        {
            PlayRoute(found, reversed, previous.camera, target.camera);
            travelTime = found.Duration;
        }
        else
            SetLiveCamera(target.camera);

        BlendFocus(target, instant);
        bool night = station == MenuStation.Map;
        if (dayNight != null)
        {
            if (instant) dayNight.Snap(night ? 1f : 0f);
            else dayNight.BlendTo(night ? 1f : 0f);
        }
        if (mapBoard != null) mapBoard.SetActive(night);

        if (panelRoutine != null) StopCoroutine(panelRoutine);
        float panelsIn = instant ? 0f : (cameraMoves ? travelTime : 0f);
        panelRoutine = StartCoroutine(SwapPanels(target, panelsIn, instant));
        StationShown?.Invoke(station, instant ? 0f : Mathf.Max(panelsIn, panelFade) + panelFade);
    }

    // Activating a camera and deactivating the rest makes the brain blend to it
    private void SetLiveCamera(CinemachineCamera live)
    {
        foreach (StationView view in stations)
            if (view.camera != null && view.camera != live)
                view.camera.gameObject.SetActive(false);
        if (travelCamera != null && travelCamera != live) travelCamera.gameObject.SetActive(false);
        if (live != null) live.gameObject.SetActive(true);
    }

    private bool TryGetRoute(MenuStation source, MenuStation destination, out CameraRoute found, out bool reversed)
    {
        found = null;
        reversed = false;
        if (routes == null || travelCamera == null) return false;
        foreach (CameraRoute candidate in routes)
        {
            if (candidate == null || !candidate.Connects(source, destination, out reversed)) continue;
            found = candidate;
            return true;
        }
        return false;
    }

    // The travel camera starts on the source camera's pose and ends on the destination's, so the
    // brain cuts to it and back (OverrideBlend) without a visible jump
    private void PlayRoute(CameraRoute next, bool reversed, CinemachineCamera source, CinemachineCamera destination)
    {
        route = next;
        routeReversed = reversed;
        routeArrived = false;
        routeDestination = destination;
        routeFrom = source.transform.position;
        routeTo = destination.transform.position;
        routeFromRotation = source.transform.rotation;
        routeToRotation = destination.transform.rotation;
        routeFromFov = source.Lens.FieldOfView;

        travelCamera.Lens = source.Lens;
        travelCamera.transform.SetPositionAndRotation(routeFrom, routeFromRotation);
        SetLiveCamera(travelCamera);

        routeTween = Tween.Custom(this, 0f, 1f, route.Duration, (menu, t) => menu.MoveTravelCamera(t), Ease.InOutSine)
            .OnComplete(this, menu => menu.ArriveRoute());
    }

    private void MoveTravelCamera(float t)
    {
        travelCamera.transform.SetPositionAndRotation(
            route.Evaluate(t, routeReversed, routeFrom, routeTo),
            Quaternion.Slerp(routeFromRotation, routeToRotation, t));
        travelCamera.Lens.FieldOfView = Mathf.Lerp(routeFromFov, routeDestination.Lens.FieldOfView, t);
    }

    private void ArriveRoute()
    {
        routeArrived = true;
        SetLiveCamera(routeDestination);
        route = null;
    }

    // Cuts into the travel camera, and out of it once a route has arrived: the poses match exactly
    // there, so a blend would only add lag. Every other switch keeps the brain's own blend
    private CinemachineBlendDefinition OverrideBlend(ICinemachineCamera fromCamera, ICinemachineCamera toCamera,
        CinemachineBlendDefinition defaultBlend, UnityEngine.Object owner)
    {
        if (travelCamera == null) return defaultBlend;
        bool intoTravel = ReferenceEquals(toCamera, travelCamera);
        bool outOfArrivedTravel = ReferenceEquals(fromCamera, travelCamera) && routeArrived;
        return intoTravel || outOfArrivedTravel ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f) : defaultBlend;
    }

    private IEnumerator SwapPanels(StationView target, float delay, bool instant)
    {
        // Hide everything that is not the target first, so nothing is clickable mid-pan
        foreach (StationView view in stations)
            foreach (CanvasGroup panel in view.panels)
                if (Array.IndexOf(target.panels, panel) < 0)
                    panel.interactable = panel.blocksRaycasts = false;

        float t = 0f;
        float fade = instant ? 0f : panelFade;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            foreach (StationView view in stations)
                foreach (CanvasGroup panel in view.panels)
                    if (Array.IndexOf(target.panels, panel) < 0)
                        panel.alpha = Mathf.Min(panel.alpha, 1f - t / fade);
            yield return null;
        }
        foreach (StationView view in stations)
            foreach (CanvasGroup panel in view.panels)
                if (Array.IndexOf(target.panels, panel) < 0)
                    SetPanel(panel, 0f);

        if (delay > fade) yield return new WaitForSecondsRealtime(delay - fade);

        t = 0f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            foreach (CanvasGroup panel in target.panels) panel.alpha = t / fade;
            yield return null;
        }
        foreach (CanvasGroup panel in target.panels) SetPanel(panel, 1f);
        panelRoutine = null;
    }

    private bool TryGetView(MenuStation station, out StationView view)
    {
        foreach (StationView candidate in stations)
        {
            if (candidate.station != station) continue;
            view = candidate;
            return true;
        }
        view = default;
        return false;
    }

    private void BlendFocus(StationView target, bool instant)
    {
        if (depthOfField == null) return;

        focusTween.Stop();
        fromStart = depthOfField.gaussianStart.value;
        fromEnd = depthOfField.gaussianEnd.value;
        toStart = target.focusStart;
        toEnd = target.focusEnd;

        if (instant) ApplyFocus(1f);
        else focusTween = Tween.Custom(this, 0f, 1f, panelDelay, (menu, t) => menu.ApplyFocus(t), Ease.InOutSine);
    }

    private void ApplyFocus(float t)
    {
        depthOfField.gaussianStart.Override(Mathf.Lerp(fromStart, toStart, t));
        depthOfField.gaussianEnd.Override(Mathf.Lerp(fromEnd, toEnd, t));
    }

    private static void SetPanel(CanvasGroup panel, float alpha)
    {
        panel.alpha = alpha;
        panel.interactable = panel.blocksRaycasts = alpha > 0.99f;
    }

    private void OnDestroy()
    {
        focusTween.Stop();
        routeTween.Stop();
        if (postProcessingVolume != null && postProcessingVolume.HasInstantiatedProfile())
            Destroy(postProcessingVolume.profile);
    }
}
