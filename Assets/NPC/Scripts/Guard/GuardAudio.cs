using UnityEngine;
using UnityEngine.Serialization;

// The guard's voice: a random line now and then while it is suspicious or searching, a shout now and then
// while it chases the player, and one random line when it catches them. Listens to GuardBehaviour and
// decides nothing. Not a NoiseEvent: other NPCs ignore it
[RequireComponent(typeof(GuardBehaviour), typeof(AudioSource))]
public class GuardAudio : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField, Tooltip("One is picked at random when the guard catches the player")]
    private AudioClip[] caughtClips;

    [SerializeField, Tooltip("Played when a chase starts, then every chase interval while it lasts")]
    private AudioClip chaseClip;

    [SerializeField, Tooltip("One is picked at random when the guard turns suspicious, then every suspicious interval while it watches or investigates. Never the same one twice in a row")]
    private AudioClip[] suspiciousClips;

    [Header("Intervals")]
    [SerializeField, Min(0.5f), Tooltip("Seconds between chase shouts")]
    private float chaseInterval = 4f;

    [SerializeField, Min(0.5f), Tooltip("Seconds between suspicious lines")]
    private float suspiciousInterval = 6f;

    [FormerlySerializedAs("chaseJitter")]
    [SerializeField, Min(0f), Tooltip("Seconds of random variation per interval, so two guards don't speak in step")]
    private float intervalJitter = 1f;

    private GuardBehaviour guard;
    private AudioSource source;
    private bool chasing;
    // Watching or Investigating, which covers searching for a player it lost in a chase
    private bool suspicious;
    private float nextLineTime;
    private int lastSuspiciousIndex = -1;

    private void Awake()
    {
        guard = GetComponent<GuardBehaviour>();
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;

        // Each sound works without the others, so a missing clip only warns
        if (caughtClips == null || caughtClips.Length == 0)
            Debug.LogWarning($"{name} has no caught clips; catching the player will be silent", this);
        if (chaseClip == null)
            Debug.LogWarning($"{name} has no chase clip; chasing will be silent", this);
        if (suspiciousClips == null || suspiciousClips.Length == 0)
            Debug.LogWarning($"{name} has no suspicious clips; watching and investigating will be silent", this);
    }

    private void OnEnable()
    {
        guard.ActivityChanged += OnActivityChanged;
        guard.Caught += OnCaught;
    }

    private void OnDisable()
    {
        guard.ActivityChanged -= OnActivityChanged;
        guard.Caught -= OnCaught;
    }

    // A timer, so it runs every frame, but bails on the first check whenever the guard is calm
    private void Update()
    {
        if (!chasing && !suspicious) return;
        if (Time.time < nextLineTime) return;

        if (chasing) Shout();
        else SaySuspicious();
    }

    // The first line of each mood comes right away, the rest on the timer. Watching to Investigating keeps
    // the running timer, so stepping up a level does not cut in with a second line
    private void OnActivityChanged(GuardActivity from, GuardActivity to)
    {
        bool wasSuspicious = suspicious;
        chasing = to == GuardActivity.Pursuing;
        suspicious = to == GuardActivity.Watching || to == GuardActivity.Investigating;

        if (chasing) Shout();
        else if (suspicious && !wasSuspicious) SaySuspicious();
    }

    // The guard's Activity stays Pursuing after a catch, so chasing is cleared here or the timer would keep
    // shouting over the failure screen. Stop cuts a line mid-word so the catch line plays clean
    private void OnCaught()
    {
        chasing = false;
        suspicious = false;
        source.Stop();

        if (caughtClips == null || caughtClips.Length == 0) return;
        Play(caughtClips[Random.Range(0, caughtClips.Length)]);
    }

    private void Shout()
    {
        Play(chaseClip);
        nextLineTime = Time.time + chaseInterval + Random.Range(-intervalJitter, intervalJitter);
    }

    private void SaySuspicious()
    {
        nextLineTime = Time.time + suspiciousInterval + Random.Range(-intervalJitter, intervalJitter);
        if (suspiciousClips == null || suspiciousClips.Length == 0) return;

        // Shifting a repeat to the next clip keeps the pick random without ever playing one twice in a row
        int index = Random.Range(0, suspiciousClips.Length);
        if (suspiciousClips.Length > 1 && index == lastSuspiciousIndex)
            index = (index + 1) % suspiciousClips.Length;

        lastSuspiciousIndex = index;
        Play(suspiciousClips[index]);
    }

    // Null-safe, since an array slot can be left empty in the Inspector
    private void Play(AudioClip clip)
    {
        if (clip != null) source.PlayOneShot(clip);
    }
}
