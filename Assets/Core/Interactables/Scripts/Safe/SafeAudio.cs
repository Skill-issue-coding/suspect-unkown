using UnityEngine;

public class SafeAudio : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField, Tooltip("Plays the dial-turning loop. Its Audio Generator is the loop clip")]
    private AudioSource loopSource;
    [SerializeField, Tooltip("Plays the number-set and open sounds. Separate, so pausing the loop never cuts them off")]
    private AudioSource oneShotSource;

    [Header("Clips")]
    [SerializeField, Tooltip("Played when a number of the combination is set")]
    private AudioClip numberSetClip;
    [SerializeField, Tooltip("Played once when the last number is set and the safe opens")]
    private AudioClip openClip;

    [Header("Dial loop")]
    [SerializeField, Min(0f), Tooltip("Seconds the dial must be still before the loop pauses")]
    private float stopDelay = 0.1f;

    private SafeModel model;
    private float lastDialAngle;
    private float stillTimer;
    private bool playing;
    private bool started;

    private void Awake()
    {
        if (loopSource == null || oneShotSource == null)
        {
            Debug.LogWarning($"{name} needs both a loop and a one-shot AudioSource; safe cracking will be silent", this);
            enabled = false;
            return;
        }

        loopSource.playOnAwake = false;
        loopSource.loop = true;
        oneShotSource.playOnAwake = false;

        if (loopSource.resource == null)
            Debug.LogWarning($"{name} has no dial clip on its loop AudioSource; turning the dial will be silent", this);
    }

    // End(), store model, reset state, subscribe NumberSet
    public void Begin(SafeModel safeModel)
    {
        if (!enabled || safeModel == null) return;

        End();
        model = safeModel;
        lastDialAngle = model.DialAngle;
        stillTimer = stopDelay;
        playing = false;
        started = false;

        model.NumberSet += OnNumberSet;
    }

    // unsubscribe, loopSource.Stop(), model = null
    public void End()
    {
        if (model == null) return;

        model.NumberSet -= OnNumberSet;
        loopSource.Stop();
        model = null;
    }

    // dial-moved check → pause/unpause loop        
    private void LateUpdate()
    {
        if (model == null) return;

        bool moved = model.DialAngle != lastDialAngle;
        lastDialAngle = model.DialAngle;
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

    // open clip if model.IsOpen, else number clip          
    private void OnNumberSet()
    {
        AudioClip clip = model.IsOpen ? openClip : numberSetClip;
        if (clip != null) oneShotSource.PlayOneShot(clip);
    }
}
