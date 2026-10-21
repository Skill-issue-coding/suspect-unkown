using UnityEngine;

// Every clip the guard's animator uses, grouped by stance. Clip names live only here: when a clip is
// renamed or a variant is added, this is the one file to edit, plus the count in GuardAnimatorParams.
// All clips are takes in NPC.fbx, imported by NpcClipImport.
public readonly struct GuardClips
{
    // Used for clips exported "In Place", which carry no root motion to measure. The Blender gaits are
    // in place, so these are their stride speeds, measured from the planted foot (2026-10-20). Guard.prefab's
    // speeds must match what the rebuild logs, or the feet slide.
    private const float FallbackPatrolSpeed = 0.93f;
    private const float FallbackInvestigateSpeed = 1.68f;
    private const float FallbackRunSpeed = 3.38f;

    // 90° over the 36-frame turn clips at 30 fps.
    private const float FallbackTurnRate = 75f;

    public readonly ClipSet Idle, Watch, Search, GiveUp, Slam;
    public readonly Gait PatrolWalk, InvestigateWalk, Run;
    public readonly TurnSet Turn;

    private GuardClips(ClipSet idle, ClipSet watch, ClipSet search, ClipSet giveUp, ClipSet slam,
            Gait patrolWalk, Gait investigateWalk, Gait run, TurnSet turn)
    {
        Idle = idle; Watch = watch; Search = search; GiveUp = giveUp; Slam = slam;
        PatrolWalk = patrolWalk; InvestigateWalk = investigateWalk; Run = run;
        Turn = turn;
    }

    public static GuardClips Load() => new GuardClips(
        idle: new ClipSet("Idle", GuardAnimatorParams.IdleVariant,
            Clip("Guard_Idle_1"),
            Clip("Guard_Idle_2")),
        watch: new ClipSet(GuardAnimatorParams.Watching, null, Clip("Guard_Watch")),
        search: new ClipSet(GuardAnimatorParams.Searching, null, Clip("Guard_Search")),
        giveUp: new ClipSet(GuardAnimatorParams.GiveUp, GuardAnimatorParams.GiveUpVariant,
            Clip("Guard_GiveUp_1"),
            Clip("Guard_GiveUp_2")),
        slam: new ClipSet(GuardAnimatorParams.Slam, null, Clip("Guard_DoorSlam")),
        patrolWalk: new Gait(Clip("Guard_Walk"), FallbackPatrolSpeed),
        investigateWalk: new Gait(Clip("Guard_InvestigateWalk"), FallbackInvestigateSpeed),
        run: new Gait(Clip("Guard_Run"), FallbackRunSpeed),
        turn: new TurnSet(Clip("Guard_TurnLeft"), Clip("Guard_TurnRight"), FallbackTurnRate)
    );

    public bool HasRequired => !Idle.IsEmpty && !PatrolWalk.IsEmpty;

    private static AnimationClip Clip(string name) => NpcClipImport.Load(name);
}
