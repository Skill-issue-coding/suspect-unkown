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
// and fades its world-space panels in once the camera is nearly there.
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

    private MenuStation current;
    private Coroutine panelRoutine;
    private InputAction backAction;

    private DepthOfField depthOfField;
    private float fromStart, fromEnd, toStart, toEnd;
    private Tween focusTween;

    public MenuStation Current => current;

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

    private void OnEnable()
    {
        if (backAction == null) return;
        backAction.performed += OnBack;
        backAction.Enable();
    }

    private void OnDisable()
    {
        if (backAction == null) return;
        backAction.performed -= OnBack;
        backAction.Disable();
    }

    private void Start()
    {
        foreach (StationView view in stations)
            foreach (CanvasGroup panel in view.panels)
                SetPanel(panel, 0f);

        Show(startStation, instant: true);
    }

    // Button targets. Parameterless so the scene builder can wire them as persistent listeners.
    public void OpenMain() => Show(MenuStation.Main);
    public void OpenSettings() => Show(MenuStation.Settings);
    public void OpenWardrobe() => Show(MenuStation.Wardrobe);
    public void OpenShop() => Show(MenuStation.Shop);
    public void OpenMap() => Show(MenuStation.Map);

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
        current = station;

        // Activating a camera and deactivating the rest makes the brain blend to it
        foreach (StationView view in stations)
            if (view.camera != null && view.camera != target.camera)
                view.camera.gameObject.SetActive(false);
        if (target.camera != null) target.camera.gameObject.SetActive(true);

        BlendFocus(target, instant);
        bool night = station == MenuStation.Map;
        if (dayNight != null)
        {
            if (instant) dayNight.Snap(night ? 1f : 0f);
            else dayNight.BlendTo(night ? 1f : 0f);
        }
        if (mapBoard != null) mapBoard.SetActive(night);

        if (panelRoutine != null) StopCoroutine(panelRoutine);
        panelRoutine = StartCoroutine(SwapPanels(target, instant ? 0f : (cameraMoves ? panelDelay : 0f), instant));
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
        if (postProcessingVolume != null && postProcessingVolume.HasInstantiatedProfile())
            Destroy(postProcessingVolume.profile);
    }
}
