using UnityEngine;

// The lockpicking minigame's sound: a loop while the pick moves, a clack when it slips, a click when the
// lock opens. Reads a LockpickModel and decides nothing. LockpickView calls Begin and End, as it does the
// HUD. Two sources, because pausing or stopping a source also cuts the one-shots playing on it
public class LockpickAudio : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField, Tooltip("Plays the pick-moving loop. Its Audio Generator is the loop clip")]
    private AudioSource loopSource;

    [SerializeField, Tooltip("Plays the slip and open sounds. A separate source, so pausing the loop never cuts them off")]
    private AudioSource oneShotSource;

    [Header("Clips")]
    [SerializeField, Tooltip("Played when the pick slips and the lock snaps back")]
    private AudioClip slipClip;

    [SerializeField, Tooltip("Played once when the lock opens")]
    private AudioClip openClip;

    [Header("Pick loop")]
    [SerializeField, Min(0f), Tooltip("Seconds the pick must be still before the loop pauses. Hides single frames with no mouse movement")]
    private float stopDelay = 0.1f;

    private LockpickModel model;
    private float lastPickAngle;
    // Seconds since the pick last moved
    private float stillTimer;
    // Loop state, so the source is only touched when the pick starts or stops moving
    private bool playing;
    // Whether the loop has been started this session. Pause and UnPause then resume mid-clip, so short
    // flicks do not restart it from its first frame
    private bool started;
    private bool openPlayed;

    private void Awake()
    {
        if (loopSource == null || oneShotSource == null)
        {
            Debug.LogWarning($"{name} needs both a loop and a one-shot AudioSource; lockpicking will be silent", this);
            enabled = false;
            return;
        }

        loopSource.playOnAwake = false;
        // The pick sound is held for as long as the pick moves, so it must loop
        loopSource.loop = true;
        oneShotSource.playOnAwake = false;

        if (loopSource.resource == null)
            Debug.LogWarning($"{name} has no pick clip on its loop AudioSource; moving the pick will be silent", this);
    }

    public void Begin(LockpickModel lockModel)
    {
        if (!enabled || lockModel == null) return;

        End();
        model = lockModel;
        lastPickAngle = model.PickAngle;
        // Starts as "still", so the loop does not blip on the first frame
        stillTimer = stopDelay;
        playing = false;
        started = false;
        openPlayed = false;

        model.Slipped += OnSlipped;
    }

    // Stops only the loop: a slip or open sound already playing on the one-shot source finishes
    public void End()
    {
        if (model == null) return;

        model.Slipped -= OnSlipped;
        loopSource.Stop();
        model = null;
    }

    // Continuous: compares the pick angle with last frame's. The sources change only on a state change
    private void LateUpdate()
    {
        if (model == null) return;

        if (!openPlayed && model.IsOpen)
        {
            openPlayed = true;
            if (openClip != null) oneShotSource.PlayOneShot(openClip);
        }

        // The model only moves the pick without tension and clamps it at its limits, so holding tension
        // or pushing against an end leaves the angle unchanged and lets the loop pause
        bool moved = model.PickAngle != lastPickAngle;
        lastPickAngle = model.PickAngle;
        stillTimer = moved ? 0f : stillTimer + Time.deltaTime;

        bool shouldPlay = stillTimer < stopDelay && !model.IsOpen;
        if (shouldPlay == playing) return;
        playing = shouldPlay;

        if (!shouldPlay)
            loopSource.Pause();
        else if (started)
            loopSource.UnPause();
        else
        {
            loopSource.Play();
            started = true;
        }
    }

    private void OnSlipped()
    {
        if (slipClip != null) oneShotSource.PlayOneShot(slipClip);
    }
}
