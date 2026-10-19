using UnityEngine;
using UnityEngine.AI;

// The clerk at home (apartment-level.md 2.2, decision 22): dozes in the armchair with a small view, stirs
// (the tell: the view eases open), stays awake a moment, then dozes again. Anything above Idle wakes them
// at once: full view, looking toward the stimulus for a while, and every later doze is shorter. Sets the
// view scale on NpcPerception, so the cone and perception change together. Asks NpcFacing for a
// facing; never turns itself. The decision window lives in NpcDecisionWindow. Reports its activity like
// any civilian (ICivilianActivity), so the indicator shows the lean and the call for the resident too.
[RequireComponent(typeof(NpcPerception), typeof(NpcSuspicion))]
[RequireComponent(typeof(NpcFacing), typeof(NavMeshAgent))]
[RequireComponent(typeof(NpcDecisionWindow))]
public class ResidentBehaviour : MonoBehaviour, ICivilianActivity
{
    [Header("Rythm")]
    [SerializeField, Tooltip("Seconds of one doze, min and max")]
    private Vector2 dozeSeconds = new Vector2(5f, 8f);

    [SerializeField, Min(0.05f), Tooltip("Seconds the view takes to open fully: the stirring tell")]
    private float stirSeconds = 0.6f;

    [SerializeField, Tooltip("Seconds awake after stirring, min and max")]
    private Vector2 awakeSeconds = new Vector2(2f, 3f);

    [Header("Dozing view")]
    [SerializeField, Range(0.05f, 1f), Tooltip("View range while dozing, as a fraction of the archetype's")]
    private float dozingRange = 0.35f;

    [SerializeField, Range(0.05f, 1f), Tooltip("Field of view while dozing, as a fraction of the archetype's")]
    private float dozingFov = 0.5f;

    [Header("Scares")]
    [SerializeField, Tooltip("Seconds awake and looking toward the stimulus after being woken")]
    private float scaredSeconds = 10f;

    [SerializeField, Range(0.1f, 1f), Tooltip("Each scare multiplies later dozes by this, so careless play gets harder")]
    private float dozeAfterScare = 0.7f;

    [SerializeField, Min(0.5f), Tooltip("Shortest a doze can get after scares, in seconds")]
    private float shortestDoze = 2f;

    [Header("Snack run")]
    [SerializeField, Tooltip("Where they stand at the fridge or counter; its forward faces the fridge. Empty: no snack runs")]
    private Transform snackPoint;

    [SerializeField, Tooltip("Seconds of dozing between runs, min and max. Only dozing counts")]
    private Vector2 snackEvery = new Vector2(45f, 75f);

    [SerializeField, Min(0.1f), Tooltip("Seconds of stretching and standing up before walking: the tell")]
    private float getUpSeconds = 1.5f;

    [SerializeField, Tooltip("Seconds at the fridge, min and max")]
    private Vector2 snackSeconds = new Vector2(4f, 6f);

    [SerializeField, Tooltip("Walking speed, metres per second")]
    private float walkSpeed = 1.2f;

    [Header("Debug")]
    [SerializeField, Tooltip("Label the phase and its timer above the clerk in the Scene view")]
    private bool debug = true;

    private NpcPerception perception;
    private NpcSuspicion suspicion;
    private NpcDecisionWindow window;
    private NpcFacing facing;
    private NavMeshAgent agent;

    private Vector3 seatPosition;
    private float scaredUntil;
    private float dozedTime;
    private float runAfter;
    private float phaseEnd;
    private float openness;
    private float dozeFactor = 1f;
    private bool scared;
    private float seatYaw;
    private CivilianActivity reported;

    public event System.Action<CivilianActivity, CivilianActivity> ActivityChanged;

    public ResidentPhase Phase { get; private set; }
    public bool IsScared => scared;
    public CivilianActivity Activity =>
            window.IsCalling ? CivilianActivity.Calling :
            window.IsDeliberating ? CivilianActivity.Deliberating :
            Phase switch
            {
                ResidentPhase.Dozing => CivilianActivity.Dozing,
                ResidentPhase.ToKitchen or ResidentPhase.Returning => CivilianActivity.Roaming,
                _ => CivilianActivity.Working,
            };

    private bool Seated => Phase is ResidentPhase.Dozing or ResidentPhase.Stirring
                                 or ResidentPhase.Awake or ResidentPhase.GettingUp;

    private void Awake()
    {
        window = GetComponent<NpcDecisionWindow>();
        perception = GetComponent<NpcPerception>();
        suspicion = GetComponent<NpcSuspicion>();
        facing = GetComponent<NpcFacing>();
        seatYaw = transform.eulerAngles.y;
        agent = GetComponent<NavMeshAgent>();
        agent.speed = walkSpeed;
        seatPosition = transform.position;
    }

    private void Start()
    {
        openness = 0f;
        ApplyView();
        runAfter = Random.Range(snackEvery.x, snackEvery.y);
        BeginDoze();
        reported = Activity;
    }

    // As CivilianBehaviour: after every Update, so a change the window made this frame is seen this frame.
    // Activity is worked out from the window and the phase, so there is no single setter to raise it from
    private void LateUpdate()
    {
        CivilianActivity now = Activity;
        if (now == reported) return;

        CivilianActivity from = reported;
        reported = now;
        ActivityChanged?.Invoke(from, now);
    }

    private void Update()
    {
        if (JobOutcome.HasEnded)
        {
            enabled = false;
            return;
        }

        if (window.IsCalling)
        {
            Face();
            return;
        }

        if (suspicion.State > NpcAlertState.Idle) Scare();
        else if (scared && Time.time >= scaredUntil) CalmDown();

        if (Phase == ResidentPhase.Dozing) dozedTime += Time.deltaTime;

        if (!scared) Advance();
        Face();
        EaseView();
    }

    private void Advance()
    {
        switch (Phase)
        {
            case ResidentPhase.Dozing when Time.time >= phaseEnd:
                bool hungry = snackPoint != null && dozedTime >= runAfter;
                Phase = hungry ? ResidentPhase.GettingUp : ResidentPhase.Stirring;
                phaseEnd = Time.time + (hungry ? getUpSeconds : stirSeconds);
                break;

            case ResidentPhase.Stirring when Time.time >= phaseEnd:
                Phase = ResidentPhase.Awake;
                phaseEnd = Time.time + Random.Range(awakeSeconds.x, awakeSeconds.y);
                break;

            case ResidentPhase.Awake when Time.time >= phaseEnd:
                BeginDoze();
                break;

            case ResidentPhase.GettingUp when Time.time >= phaseEnd:
                Phase = ResidentPhase.ToKitchen;
                agent.SetDestination(snackPoint.position);
                break;

            case ResidentPhase.ToKitchen when agent.HasArrived():
                Phase = ResidentPhase.Snacking;
                phaseEnd = Time.time + Random.Range(snackSeconds.x, snackSeconds.y);
                break;

            case ResidentPhase.Snacking when Time.time >= phaseEnd:
                Phase = ResidentPhase.Returning;
                agent.SetDestination(seatPosition);
                break;

            case ResidentPhase.Returning when agent.HasArrived():
                // Sat down awake: the awake spell, and the next run counts from here
                Phase = ResidentPhase.Awake;
                phaseEnd = Time.time + Random.Range(awakeSeconds.x, awakeSeconds.y);
                dozedTime = 0f;
                runAfter = Random.Range(snackEvery.x, snackEvery.y);
                break;
        }
    }

    private void BeginDoze()
    {
        Phase = ResidentPhase.Dozing;
        float length = Random.Range(dozeSeconds.x, dozeSeconds.y) * dozeFactor;
        phaseEnd = Time.time + Mathf.Max(shortestDoze, length);
    }

    // The phase sets a target; openness eases toward it at one full opening per stirSeconds, and the
    // perception scale follows. Bails while it is already there, which is most frames
    private void EaseView()
    {
        float target = Phase == ResidentPhase.Dozing ? 0f : 1f;
        if (openness == target) return;

        openness = Mathf.MoveTowards(openness, target, Time.deltaTime / stirSeconds);
        ApplyView();
    }

    private void ApplyView() => perception.SetViewScale(
        Mathf.Lerp(dozingRange, 1f, openness),
        Mathf.Lerp(dozingFov, 1f, openness));

    // While walking, no request: NpcFacing turns along the path
    private void Face()
    {
        if (scared && suspicion.HasStimulus) facing.LookAt(suspicion.LastStimulus);
        else if (Phase == ResidentPhase.Snacking) facing.FaceYaw(snackPoint.eulerAngles.y);
        else if (Seated) facing.FaceYaw(seatYaw);
    }

    // Anything above Idle: stop, look toward the stimulus, and stay that way until scaredSeconds after the
    // last suspicious moment. In the armchair that also wakes them. The first frame shortens later dozes
    private void Scare()
    {
        if (!scared)
        {
            dozeFactor *= dozeAfterScare;
            agent.isStopped = true;
        }
        scared = true;
        scaredUntil = Time.time + scaredSeconds;

        if (Phase is ResidentPhase.Dozing or ResidentPhase.Stirring)
            Phase = ResidentPhase.Awake;
    }

    // Carry on with whatever the scare paused; an awake spell already past its end dozes off next frame
    private void CalmDown()
    {
        scared = false;
        agent.isStopped = false;
    }

    private void OnDrawGizmos()
    {
        if (!debug || !Application.isPlaying) return;
#if UNITY_EDITOR
        string label = $"{Phase}  {Mathf.Max(0f, phaseEnd - Time.time):F1}s  view {openness:P0}";
        if (scared) label += "  (scared)";
        if (window.IsCalling) label += "  CALLING";
        else if (window.IsDeliberating) label += $"  window {window.Progress:P0}";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.2f, label);
#endif
    }
}
