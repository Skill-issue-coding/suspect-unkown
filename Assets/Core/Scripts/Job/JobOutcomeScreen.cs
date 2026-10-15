using System.Collections;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shows how a job ended. On failure the picture turns grey, time slows, and the newspaper spins in
// with a "caught" headline and a restart button; on success it comes in with a positive headline and
// a continue button. The photo shows the witness sketch from JobNews: a mugshot when caught, a wanted
// picture when the player got away, a "?" when nobody saw them. Listens to JobOutcome and JobNews only,
// so it never needs to know who ended the job or who saw the player. Lives on the always-active canvas
// root, because an inactive object never receives the event
public class JobOutcomeScreen : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Everything visible. Inactive until a job ends")]
    private GameObject screenRoot;

    [SerializeField, Tooltip("The sheet that slides and spins in")]
    private RectTransform paper;

    [SerializeField, Tooltip("Receives the random headline")]
    private TMP_Text headline;

    [SerializeField, Tooltip("Darkens the game behind the paper")]
    private CanvasGroup backdrop;

    [SerializeField, Tooltip("Faded in after the paper lands, and only then clickable")]
    private CanvasGroup buttons;

    [SerializeField, Tooltip("Shown on failure")]
    private GameObject restartButton;

    [SerializeField, Tooltip("Shown on success")]
    private GameObject continueButton;

    [Header("Headlines")]
    [SerializeField, Tooltip("A guard caught the player")]
    private HeadlineSet caught;

    [SerializeField, Tooltip("A civilian finished a call")]
    private HeadlineSet policeCalled;

    [SerializeField, Tooltip("The job succeeded")]
    private HeadlineSet completed;

    [Header("Failure")]
    [SerializeField, Tooltip("Global volume with saturation -100, weight 0. Faded to 1 on failure")]
    private Volume failVolume;

    [SerializeField, Tooltip("Seconds, unscaled, for the picture to turn grey")]
    private float greyDuration = 0.8f;

    [SerializeField, Tooltip("Time scale the world slows to"), Range(0.05f, 1f)]
    private float slowTimeScale = 0.2f;

    [SerializeField, Tooltip("Seconds, unscaled, to reach the slow time scale")]
    private float slowDuration = 0.8f;

    [Header("Paper entry")]
    [SerializeField, Tooltip("Seconds, unscaled, before the paper arrives, so the grey and slowdown read first")]
    private float paperDelay = 0.5f;

    [SerializeField, Tooltip("Seconds, unscaled, for the paper to slide, grow and spin in")]
    private float paperDuration = 0.7f;

    [SerializeField, Tooltip("Degrees the paper spins while coming in")]
    private float paperSpin = 720f;

    [SerializeField, Tooltip("Degrees the paper rests at, slightly tilted")]
    private float paperRestAngle = -4f;

    [SerializeField, Tooltip("Where the paper starts, relative to its resting place")]
    private Vector2 paperStartOffset = new Vector2(0f, -900f);

    [SerializeField, Tooltip("Scale the paper starts at")]
    private float paperStartScale = 0.2f;

    [Header("Scenes")]
    [SerializeField, Tooltip("Scene loaded by Continue. Must be in the build scene list")]
    private string menuScene = "MainMenu";

    [Header("Witness sketch")]
    [SerializeField, Tooltip("Shows the witness sketch's RenderTexture through the grayscale material")]
    private RawImage sketch;

    [SerializeField, Tooltip("The \"?\" shown when nobody saw the player")]
    private GameObject noWitnessMark;

    [SerializeField, Tooltip("Police height lines behind the figure: shown when the player was caught")]
    private GameObject mugshotLines;

    [SerializeField, Tooltip("The caption under the photo")]
    private TMP_Text caption;

    [SerializeField, Tooltip("Caption title when the job failed")]
    private string caughtTitle = "Image of the caught criminal.";

    [SerializeField, Tooltip("Caption title when the job succeeded but someone saw the player")]
    private string wantedTitle = "Police seek this person.";

    [SerializeField, Tooltip("Caption when nobody saw the player")]
    private string noWitnessCaption = "No witnesses came forward.";

    private Tween greyTween;
    private Tween timeTween;
    private Sequence entry;
    private bool failed;

    private void Awake()
    {
        if (screenRoot == null || paper == null || headline == null || backdrop == null || buttons == null ||
            restartButton == null || continueButton == null || failVolume == null ||
            caught == null || policeCalled == null || completed == null ||
            sketch == null || noWitnessMark == null || mugshotLines == null || caption == null)
        {
            Debug.LogWarning($"{name} is missing references; job outcomes will not be shown", this);
            enabled = false;
            return;
        }

        screenRoot.SetActive(false);
    }

    private void OnEnable()
    {
        JobOutcome.Failed += OnFailed;
        JobOutcome.Completed += OnCompleted;
        JobNews.Changed += ShowPhoto;
    }

    private void OnDisable()
    {
        JobOutcome.Failed -= OnFailed;
        JobOutcome.Completed -= OnCompleted;
        JobNews.Changed -= ShowPhoto;
    }

    private void OnFailed(FailReason reason)
    {
        greyTween = Tween.Custom(failVolume, 0f, 1f, greyDuration,
            (volume, weight) => volume.weight = weight, useUnscaledTime: true);
        timeTween = Tween.GlobalTimeScale(slowTimeScale, slowDuration);

        failed = true;
        Show(reason == FailReason.Caught ? caught : policeCalled, restartButton);
    }

    private void OnCompleted()
    {
        failed = false;
        Show(completed, continueButton);
    }

    private void Show(HeadlineSet set, GameObject button)
    {
        headline.text = set.PickRandom();
        ShowPhoto();
        restartButton.SetActive(button == restartButton);
        continueButton.SetActive(button == continueButton);
        screenRoot.SetActive(true);

        // The lockpick view locks and hides the cursor, and the job can end mid-pick
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Vector3 restAngles = new Vector3(0f, 0f, paperRestAngle);
        Vector3 startAngles = new Vector3(0f, 0f, paperRestAngle - paperSpin);

        paper.anchoredPosition = paperStartOffset;
        paper.localScale = Vector3.one * paperStartScale;
        paper.localEulerAngles = startAngles;
        backdrop.alpha = 0f;
        buttons.alpha = 0f;
        buttons.interactable = false;

        entry = Sequence.Create(useUnscaledTime: true)
            .ChainDelay(paperDelay)
            .Chain(Tween.Alpha(backdrop, 1f, 0.3f))
            .Group(Tween.UIAnchoredPosition(paper, Vector2.zero, paperDuration, Ease.OutCubic))
            .Group(Tween.Scale(paper, 1f, paperDuration, Ease.OutBack))
            .Group(Tween.LocalEulerAngles(paper, startAngles, restAngles, paperDuration, Ease.OutCubic))
            .Chain(Tween.Alpha(buttons, 1f, 0.25f))
            .ChainCallback(this, self => self.buttons.interactable = true);
    }

    // The photo box: the witness sketch with a title for how the job ended, or the "?" when nobody saw the
    // player. Called when the paper is shown and again on JobNews.Changed, because the description and the
    // outcome arrive on the same event in no fixed order: whichever comes second leaves it right
    private void ShowPhoto()
    {
        bool seen = JobNews.HasWitness;
        sketch.enabled = seen;
        noWitnessMark.SetActive(!seen);
        mugshotLines.SetActive(seen && failed);
        caption.text = seen ? $"<b>{(failed ? caughtTitle : wantedTitle)}</b> {JobNews.WitnessCaption}" : noWitnessCaption;
    }

    // Connected to RestartButton's On Click
    public void Restart() => Leave(SceneManager.GetActiveScene().name);

    // Connected to ContinueButton's On Click
    public void Continue() => Leave(menuScene);

    private void Leave(string scene)
    {
        greyTween.Stop();
        timeTween.Stop();
        entry.Stop();
        Time.timeScale = 1f;

        SceneManager.LoadScene(scene);
    }

    // The context menu freezes the game while it is open, and that wait arrives as one long frame of
    // real time, which would swallow the unscaled entry. Firing two frames later keeps the test honest
    [ContextMenu("Test Fail")]
    private void TestFail() => StartCoroutine(FireLater(true));

    [ContextMenu("Test Complete")]
    private void TestComplete() => StartCoroutine(FireLater(false));

    private IEnumerator FireLater(bool fail)
    {
        yield return null;
        yield return null;
        if (fail) JobOutcome.Fail(FailReason.PoliceCalled);
        else JobOutcome.Complete();
    }
}
