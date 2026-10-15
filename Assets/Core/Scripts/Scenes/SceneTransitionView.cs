using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// The loading card SceneTransition shows between scenes: fades in over the old scene (and fades the sound out),
// loads the next scene in the background while a bar shows the progress, a suspect sketch is drawn in from the
// bottom under a sweeping scan line, and gameplay tips fade in and out one after another, stays up a minimum time so a fast load does not flash, lets the new scene start behind it,
// then fades out. Runs on unscaled time, because the
// failure newspaper slows time down. Lives on the SceneTransition prefab in Resources, kept across scenes
public class SceneTransitionView : MonoBehaviour
{
    [Serializable]
    public struct Destination
    {
        [Tooltip("Scene name, as in the build scene list")]
        public string scene;
        [Tooltip("Title on the card while loading it")]
        public string title;
    }

    [Header("Parts")]
    [SerializeField, Tooltip("Group over the whole card: faded, and blocks clicks while up")]
    private CanvasGroup cover;

    [SerializeField, Tooltip("Destination title")]
    private TMP_Text title;

    [SerializeField, Tooltip("Gameplay tip, a different one each load")]
    private TMP_Text tip;

    [SerializeField, Tooltip("Progress bar fill. Its right anchor is set to the shown progress, 0 to 1")]
    private RectTransform barFill;

    [SerializeField, Tooltip("Suspect sketch, a Filled image that fills from the bottom with the progress")]
    private UnityEngine.UI.Image sketch;

    [SerializeField, Tooltip("Scan line that sweeps up and down over the sketch while the card is up")]
    private RectTransform scanLine;

    [Header("Content")]
    [SerializeField, Tooltip("Card titles per scene. A scene not listed shows its own name in capitals")]
    private Destination[] destinations;

    [SerializeField, Tooltip("Tips shown at random, never the same twice in a row")]
    private string[] tips;

    [Header("Timing")]
    [SerializeField, Tooltip("Seconds the card takes to cover the old scene")]
    private float coverSeconds = 0.4f;

    [SerializeField, Tooltip("Seconds the card stays up at least, so a fast load does not flash")]
    private float minShownSeconds = 0.6f;

    [SerializeField, Tooltip("Seconds the card takes to uncover the new scene")]
    private float revealSeconds = 0.5f;

    [SerializeField, Tooltip("How fast the shown bar catches up with the real progress, bar widths per second")]
    private float barSpeed = 2.5f;

    [SerializeField, Tooltip("Seconds a tip stays fully shown before the next one replaces it")]
    private float tipSeconds = 3.5f;

    [SerializeField, Tooltip("Seconds a tip takes to fade in or out")]
    private float tipFade = 0.35f;

    [SerializeField, Tooltip("Seconds the scan line takes to sweep from the bottom of the sketch to the top")]
    private float scanSeconds = 1.2f;

    private int lastTip = -1;
    private float shownProgress;
    private Coroutine tipRoutine, scanRoutine;

    public bool IsLoading { get; private set; }

    private void Awake()
    {
        if (cover == null || barFill == null)
        {
            Debug.LogWarning($"{name} is missing its cover or bar", this);
            enabled = false;
            return;
        }
        DontDestroyOnLoad(gameObject);
        SetCover(0f);
    }

    public void Begin(string scene)
    {
        if (!enabled)
        {
            SceneManager.LoadScene(scene);
            return;
        }
        IsLoading = true;
        StartCoroutine(Run(scene));
    }

    private IEnumerator Run(string scene)
    {
        if (title != null) title.text = TitleFor(scene);
        shownProgress = 0f;
        SetBar(0f);
        cover.blocksRaycasts = true;
        if (tip != null)
        {
            tip.alpha = 0f;
            tipRoutine = StartCoroutine(CycleTips());
        }
        if (scanLine != null) scanRoutine = StartCoroutine(Scan());

        // The listener volume is the master volume setting: restored exactly once the new scene shows
        float volume = AudioListener.volume;
        yield return Fade(0f, 1f, coverSeconds, volume);

        float shownAt = Time.unscaledTime;
        AsyncOperation load = SceneManager.LoadSceneAsync(scene);
        load.allowSceneActivation = false;

        // Unity stops at 0.9 until it may switch, so 0.9 is a full bar
        while (load.progress < 0.9f || shownProgress < 1f || Time.unscaledTime - shownAt < minShownSeconds)
        {
            AdvanceBar(Mathf.Clamp01(load.progress / 0.9f));
            yield return null;
        }

        load.allowSceneActivation = true;
        while (!load.isDone) yield return null;
        yield return null; // one frame for the new scene's Start to run behind the card

        // The tip goes out with the card; a tip half faded in simply fades with it
        if (tipRoutine != null) StopCoroutine(tipRoutine);
        tipRoutine = null;
        yield return Fade(1f, 0f, revealSeconds, volume);
        if (scanRoutine != null) StopCoroutine(scanRoutine);
        scanRoutine = null;
        cover.blocksRaycasts = false;
        IsLoading = false;
    }

    // Card alpha from one value to the other; the sound fades the opposite way
    private IEnumerator Fade(float from, float to, float seconds, float volume)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float a = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
            SetCover(a);
            AudioListener.volume = volume * (1f - a);
            yield return null;
        }
        SetCover(to);
        AudioListener.volume = volume * (1f - to);
    }

    // Fades a tip in, holds it, fades it out and moves to the next, for as long as the card is up. A quick load
    // shows one tip; a slow one cycles through several
    private IEnumerator CycleTips()
    {
        while (true)
        {
            tip.text = NextTip();
            yield return FadeTip(0f, 1f);
            yield return new WaitForSecondsRealtime(tipSeconds);
            yield return FadeTip(1f, 0f);
        }
    }

    private IEnumerator FadeTip(float from, float to)
    {
        for (float t = 0f; t < tipFade; t += Time.unscaledDeltaTime)
        {
            tip.alpha = Mathf.Lerp(from, to, t / tipFade);
            yield return null;
        }
        tip.alpha = to;
    }

    // Sweeps the scan line up and down the sketch, easing at each end, for as long as the card is up
    private IEnumerator Scan()
    {
        RectTransform area = (RectTransform)scanLine.parent;
        float half = area.rect.height * 0.5f;
        for (float t = 0f; ; t += Time.unscaledDeltaTime)
        {
            float pingPong = Mathf.PingPong(t / scanSeconds, 1f);
            float y = Mathf.Lerp(-half, half, Mathf.SmoothStep(0f, 1f, pingPong));
            scanLine.anchoredPosition = new Vector2(scanLine.anchoredPosition.x, y);
            yield return null;
        }
    }

    // Smooth what is displayed, never the real progress
    private void AdvanceBar(float progress)
    {
        shownProgress = Mathf.MoveTowards(shownProgress, progress, barSpeed * Time.unscaledDeltaTime);
        SetBar(shownProgress);
    }

    // The bar and the sketch show the same progress: the suspect is drawn in as the scene loads
    private void SetBar(float progress)
    {
        barFill.anchorMax = new Vector2(progress, barFill.anchorMax.y);
        if (sketch != null) sketch.fillAmount = progress;
    }

    private void SetCover(float alpha) => cover.alpha = alpha;

    private string TitleFor(string scene)
    {
        if (destinations != null)
            foreach (Destination destination in destinations)
                if (destination.scene == scene) return destination.title;
        return scene.ToUpperInvariant();
    }

    private string NextTip()
    {
        if (tips == null || tips.Length == 0) return "";
        int index = UnityEngine.Random.Range(0, tips.Length);
        if (tips.Length > 1 && index == lastTip) index = (index + 1) % tips.Length;
        lastTip = index;
        return tips[index];
    }
}
