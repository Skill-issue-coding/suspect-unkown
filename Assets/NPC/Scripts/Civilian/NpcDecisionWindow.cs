using UnityEngine;

// The decision window any civilian shares (concept 6.8, decision 22), extracted from ResidentBehaviour so
// the clerk at home, the clerk at work and customers use one implementation. It opens once when suspicion
// reaches Alerted, fills while the NPC sees the player and drains while it does not. A full window starts
// a call, which fails the job (PoliceCalled) after a delay. It decides nothing about how the NPC looks or
// moves: roles read IsDeliberating and IsCalling and choose their own body language. Its two dependencies
// are guaranteed by RequireComponent, so it does not null-check them (CLAUDE.md section 2)
[RequireComponent(typeof(NpcPerception), typeof(NpcSuspicion))]
public class NpcDecisionWindow : MonoBehaviour
{
    [SerializeField, Min(0.1f), Tooltip("Seconds of seeing the player to fill the window and start calling")]
    private float windowSeconds = 2.5f;

    [SerializeField, Min(0f), Tooltip("Seconds from starting the call until it connects and the job fails")]
    private float callSeconds = 1.5f;

    private NpcPerception perception;
    private NpcSuspicion suspicion;
    private float callConnects = -1f;

    public bool IsDeliberating { get; private set; }
    public float Progress { get; private set; }
    public bool IsCalling => callConnects >= 0f;

    private void Awake()
    {
        perception = GetComponent<NpcPerception>();
        suspicion = GetComponent<NpcSuspicion>();
    }

    private void OnEnable() => suspicion.AlertStateChanged += OnAlertStateChanged;

    private void OnDisable() => suspicion.AlertStateChanged -= OnAlertStateChanged;

    private void Update()
    {
        if (JobOutcome.HasEnded)
        {
            enabled = false;
            return;
        }

        if (IsCalling)
        {
            if (Time.time >= callConnects) JobOutcome.Fail(FailReason.PoliceCalled);
            return;
        }

        if (IsDeliberating) Deliberate();
    }

    // Opens on reaching Alerted, once; it does not reopen until suspicion drops and climbs again
    private void OnAlertStateChanged(NpcAlertState from, NpcAlertState to)
    {
        if (to != NpcAlertState.Alerted || IsDeliberating || IsCalling) return;
        IsDeliberating = true;
        Progress = 0f;
    }

    // Fills while the player is in sight, drains while not. Full: the call starts. Empty, with the player
    // out of sight: they lost them
    private void Deliberate()
    {
        bool seesPlayer = perception.TryGetLatest(out _);
        Progress = Mathf.MoveTowards(Progress, seesPlayer ? 1f : 0f, Time.deltaTime / windowSeconds);

        if (Progress >= 1f)
        {
            IsDeliberating = false;
            callConnects = Time.time + callSeconds;
        }
        else if (Progress <= 0f && !seesPlayer)
            IsDeliberating = false;
    }
}
