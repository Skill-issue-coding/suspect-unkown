using UnityEngine;

// Every clip the guard's animator uses, grouped by stance. Clip paths live only here: when a clip
// moves or a variant is added, this is the one file to edit, plus the count in GuardAnimatorParams.
public readonly struct GuardClips
{
    private const string Root = "Assets/NPC/Art/Guard/Animation Clips";

    // Used for clips exported "In Place", which carry no root motion to measure. GuardBehaviour's
    // speeds must match what the rebuild logs, or the feet slide.
    private const float FallbackPatrolSpeed = 1.5f;
    private const float FallbackInvestigateSpeed = 2.5f;
    private const float FallbackRunSpeed = 4.5f;

    // 90° over the 36-frame turn clips at 30 fps.
    private const float FallbackTurnRate = 75f;

    public readonly ClipSet Idle, Watch, Search, GiveUp;
    public readonly Gait PatrolWalk, InvestigateWalk, Run;
    public readonly TurnSet Turn;

    private GuardClips(ClipSet idle, ClipSet watch, ClipSet search, ClipSet giveUp,
            Gait patrolWalk, Gait investigateWalk, Gait run, TurnSet turn)
    {
        Idle = idle; Watch = watch; Search = search; GiveUp = giveUp;
        PatrolWalk = patrolWalk; InvestigateWalk = investigateWalk; Run = run;
        Turn = turn;
    }

    public static GuardClips Load() => new GuardClips(
        idle: new ClipSet("Idle", GuardAnimatorParams.IdleVariant,
            Clip("Patrolling/guard_patrolling_idle_1.fbx"),
            Clip("Patrolling/guard_patrolling_idle_2.fbx")),
        watch: new ClipSet(GuardAnimatorParams.Watching, null, Clip("Watching/guard_watching.fbx")),
        search: new ClipSet(GuardAnimatorParams.Searching, null, Clip("Investigating/guard_investigating_looking.fbx")),
        giveUp: new ClipSet(GuardAnimatorParams.GiveUp, GuardAnimatorParams.GiveUpVariant,
            Clip("Transitions/guard_giving_up_1.fbx"),
            Clip("Transitions/guard_giving_up_2.fbx")),
        patrolWalk: new Gait(Clip("Patrolling/guard_patrol_walking.fbx"), FallbackPatrolSpeed),
        investigateWalk: new Gait(Clip("Investigating/guard_investigating_walk.fbx"), FallbackInvestigateSpeed),
        run: new Gait(Clip("Pursuing/guard_pursuing_run.fbx"), FallbackRunSpeed),
        turn: new TurnSet(Clip("Turn/guard_left_turn.fbx"), Clip("Turn/guard_right_turn.fbx"), FallbackTurnRate)
    );

    public bool HasRequired => !Idle.IsEmpty && !PatrolWalk.IsEmpty;

    private static AnimationClip Clip(string file) => AnimatorBuildUtility.LoadClip(Root, file);
}
