using UnityEngine;

// The apartment clerk's voice: a snore once each time they nod off, cut short if they wake first, and a
// random line now and then while something has them on edge. Silent while deliberating or on the phone.
// Listens to ResidentBehaviour and decides nothing. Not a NoiseEvent: other NPCs ignore it
[RequireComponent(typeof(ResidentBehaviour), typeof(NpcDecisionWindow), typeof(AudioSource))]
public class ResidentAudio : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField, Tooltip("Played once each time the clerk nods off. Cut short when they stir or are woken")]
    private AudioClip dozeClip;

    [SerializeField, Tooltip("One is picked at random when the clerk is woken or put on edge, then every suspicious interval while it lasts. Never the same one twice in a row")]
    private AudioClip[] suspiciousClips;

    [Header("Intervals")]
    [SerializeField, Min(0.5f), Tooltip("Seconds between suspicious lines")]
    private float suspiciousInterval = 6f;

    [SerializeField, Min(0f), Tooltip("Seconds of random variation per interval")]
    private float intervalJitter = 1f;

    private ResidentBehaviour resident;
    private NpcDecisionWindow window;
    private AudioSource source;
    private bool suspicious;
    // Whether the doze clip may still be playing, so only the snore is stopped and never a line
    private bool snoring;
    private float nextLineTime;
    private int lastSuspiciousIndex = -1;

    private void Awake()
    {
        resident = GetComponent<ResidentBehaviour>();
        window = GetComponent<NpcDecisionWindow>();
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;

        // Each sound works without the other, so a missing clip only warns
        if (dozeClip == null)
            Debug.LogWarning($"{name} has no doze clip; dozing off will be silent", this);
        if (suspiciousClips == null || suspiciousClips.Length == 0)
            Debug.LogWarning($"{name} has no suspicious clips; a scared clerk will be silent", this);
    }

    private void OnEnable() => resident.ActivityChanged += OnActivityChanged;

    private void OnDisable() => resident.ActivityChanged -= OnActivityChanged;

    // The clerk starts the level asleep without raising ActivityChanged, so the first doze is caught here
    private void Start()
    {
        if (resident.Activity == CivilianActivity.Dozing) OnActivityChanged(CivilianActivity.Working, CivilianActivity.Dozing);
    }

    // ResidentBehaviour has no event for being scared, so the flag is polled; the timer runs here as well.
    // Both are a few bool checks while the clerk is calm
    private void Update()
    {
        if (JobOutcome.HasEnded)
        {
            source.Stop();
            enabled = false;
            return;
        }

        bool nowSuspicious = resident.IsScared && !window.IsDeliberating && !window.IsCalling;
        if (nowSuspicious != suspicious)
        {
            suspicious = nowSuspicious;
            // The first line comes right away, the rest on the timer
            if (suspicious) SaySuspicious();
            return;
        }

        if (suspicious && Time.time >= nextLineTime) SaySuspicious();
    }

    private void OnActivityChanged(CivilianActivity from, CivilianActivity to)
    {
        if (to == CivilianActivity.Dozing)
        {
            Play(dozeClip);
            snoring = dozeClip != null;
        }
        else if (from == CivilianActivity.Dozing)
        {
            StopSnore();
        }
    }

    private void SaySuspicious()
    {
        // A scare and the end of the doze can land in either order in one frame, so the line stops the
        // snore itself rather than leaving it to OnActivityChanged, which would then cut the line
        StopSnore();
        nextLineTime = Time.time + suspiciousInterval + Random.Range(-intervalJitter, intervalJitter);
        if (suspiciousClips == null || suspiciousClips.Length == 0) return;

        // Shifting a repeat to the next clip keeps the pick random without ever playing one twice in a row
        int index = Random.Range(0, suspiciousClips.Length);
        if (suspiciousClips.Length > 1 && index == lastSuspiciousIndex)
            index = (index + 1) % suspiciousClips.Length;

        lastSuspiciousIndex = index;
        Play(suspiciousClips[index]);
    }

    private void StopSnore()
    {
        if (!snoring) return;
        snoring = false;
        source.Stop();
    }

    // Null-safe, since an array slot can be left empty in the Inspector
    private void Play(AudioClip clip)
    {
        if (clip != null) source.PlayOneShot(clip);
    }
}
