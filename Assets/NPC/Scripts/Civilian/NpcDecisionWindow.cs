using System;
using UnityEngine;

// The decision window any civilian shares (concept 6.8, decision 22), extracted from ResidentBehaviour so
// the clerk at home, the clerk at work and customers use one implementation. It opens once when suspicion
// reaches Alerted, fills while the NPC sees the player and drains while it does not. When it opens, the
// archetype's reaction tree picks the outcome. When it fills, a call starts here and fails the job after
// a delay; any other outcome is raised as Decided for the role. With no tree, the outcome is always a call.
// It decides nothing about how the NPC looks or moves: roles read IsDeliberating, PendingOutcome and
// IsCalling and choose their own body language. Its two dependencies are guaranteed by RequireComponent,
// so it does not null-check them (CLAUDE.md section 2)
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

    // What the NPC will do if the window fills, sampled once when it opens so body language can lean
    // toward it for the whole window. Valid while IsDeliberating
    public CivilianActivity PendingOutcome { get; private set; }

    // The window filled on an outcome other than a call; the role carries it out. A call stays here,
    // since this component owns the police timer
    public event Action<CivilianActivity> Decided;

    // For the readout: what the tree was given, the leaf it reached and the outcome's share of that leaf,
    // kept after the window closes so the last decision stays visible. Leaf is null without a tree
    public ReactionContext Context { get; private set; }
    public ReactionLeaf PendingLeaf { get; private set; }
    public float PendingChance { get; private set; }

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

    // Opens on reaching Alerted, once; it does not reopen until suspicion drops and climbs again. The
    // outcome is chosen here, not when the window fills
    private void OnAlertStateChanged(NpcAlertState from, NpcAlertState to)
    {
        if (to != NpcAlertState.Alerted || IsDeliberating || IsCalling) return;
        IsDeliberating = true;
        Progress = 0f;
        ChooseOutcome();
    }

    // Fills while the player is in sight, drains while not. Full: the pending outcome happens. Empty, with
    // the player out of sight: they lost them, and nothing happens
    private void Deliberate()
    {
        bool seesPlayer = perception.TryGetLatest(out _);
        Progress = Mathf.MoveTowards(Progress, seesPlayer ? 1f : 0f, Time.deltaTime / windowSeconds);

        if (Progress >= 1f)
        {
            IsDeliberating = false;
            if (PendingOutcome == CivilianActivity.Calling) callConnects = Time.time + callSeconds;
            else Decided?.Invoke(PendingOutcome);
        }
        else if (Progress <= 0f && !seesPlayer)
            IsDeliberating = false;
    }

    // No tree, or a tree that reaches no usable leaf: a call, as every window did before the tree
    private void ChooseOutcome()
    {
        PendingOutcome = CivilianActivity.Calling;
        PendingLeaf = null;
        PendingChance = 1f;

        ReactionTree tree = perception.Archetype != null ? perception.Archetype.ReactionTree : null;
        if (tree == null) return;

        Context = ReactionContext.Build(transform.position, perception, suspicion);
        if (tree.TrySample(Context, out ReactionLeaf leaf, out CivilianActivity outcome, out float chance))
        {
            PendingLeaf = leaf;
            PendingOutcome = outcome;
            PendingChance = chance;
        }
    }
}
