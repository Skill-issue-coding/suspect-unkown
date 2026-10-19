using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

// A civilian's own life (decision 22): picks a free CivilianSpot for its role, walks there, stays a
// random time facing the spot's forward, and picks again. Customers roam, staff work. From Investigating
// up it stops and looks at the stimulus, which is also how it stands while its decision window runs.
// When the window fills on an outcome other than a call (the call is NpcDecisionWindow's), it carries it
// out: Scared freezes, blinds the civilian and calms down after a random time, back to the same spot;
// Avoiding walks away from the player, then resumes the routine. Asks NpcFacing for a facing; never
// turns itself
[RequireComponent(typeof(NavMeshAgent), typeof(NpcSuspicion))]
[RequireComponent(typeof(NpcFacing), typeof(NpcDecisionWindow))]
[RequireComponent(typeof(NpcPerception))]
public class CivilianBehaviour : MonoBehaviour, ICivilianActivity
{
    [Header("Role")]
    [SerializeField, Tooltip("Customer browses; Employee and Manager work. Also decides which spots it uses")]
    private CivilianRole role = CivilianRole.Customer;

    [Header("Routine")]
    [SerializeField, Tooltip("Metres per second between spots")]
    private float walkSpeed = 1.2f;

    [SerializeField, Tooltip("Seconds before trying again when no spot is free")]
    private float retryDelay = 2f;

    [Header("Reactions")]
    [SerializeField, Tooltip("Minimum and maximum seconds a scared civilian stays frozen and blind before calming down")]
    private Vector2 scaredSeconds = new Vector2(8f, 10f);

    [SerializeField, Min(0f), Tooltip("Metres a civilian walks away from the player when avoiding")]
    private float avoidDistance = 6f;

    [Header("Debug")]
    [SerializeField, Tooltip("Show the activity, spot and reaction readout above the civilian in the Scene view")]
    private bool debug = true;

    public CivilianRole Role => role;

    // The window's state first, so the animator and the label read one source; activity is what the
    // routine or the last outcome set
    public CivilianActivity Activity =>
        window.IsCalling ? CivilianActivity.Calling :
        window.IsDeliberating ? CivilianActivity.Deliberating :
        activity;

    // Standing at the claimed spot, not stopped by a scare: the animation plays the spot's station clip
    public bool IsDwelling => spot != null && arrived && !stopped;

    // The claimed spot's station, read by the animation only while dwelling
    public CivilianStation Station => spot != null ? spot.Station : default;

    // Raised when AssignRole changes the role, so what depends on it (the outfit) can follow
    public event System.Action<CivilianRole, CivilianRole> RoleChanged;

    // From and to, once per change, including the ones the window makes (Deliberating, Calling). Read by
    // the suspicion indicator's icon and the HUD's reaction reporter
    public event System.Action<CivilianActivity, CivilianActivity> ActivityChanged;

    private readonly List<CivilianSpot> candidates = new();

    private NavMeshAgent agent;
    private NpcSuspicion suspicion;
    private NpcFacing facing;
    private NpcPerception perception;
    private NpcDecisionWindow window;

    private CivilianSpot spot;
    private bool arrived;
    private bool stopped;
    private float leaveTime;
    private float nextChooseTime;
    private float calmTime;
    private CivilianActivity activity;
    private CivilianActivity reported;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        suspicion = GetComponent<NpcSuspicion>();
        facing = GetComponent<NpcFacing>();
        perception = GetComponent<NpcPerception>();
        window = GetComponent<NpcDecisionWindow>();

        agent.speed = walkSpeed;
        activity = ActivityFor(role);
        reported = activity;
    }

    private static CivilianActivity ActivityFor(CivilianRole role) =>
        role == CivilianRole.Customer ? CivilianActivity.Roaming : CivilianActivity.Working;

    // For a spawner that builds every role from one prefab: call it right after Instantiate, before the
    // first Update, since the role decides which CivilianSpots this civilian claims
    public void AssignRole(CivilianRole newRole)
    {
        if (newRole == role) return;

        CivilianRole previous = role;
        role = newRole;
        activity = ActivityFor(role);
        reported = activity;
        ReleaseSpot();
        RoleChanged?.Invoke(previous, role);
    }

    private void OnEnable() => window.Decided += OnDecided;

    private void OnDisable()
    {
        window.Decided -= OnDecided;
        ReleaseSpot();
    }

    private void ReleaseSpot()
    {
        if (spot != null) spot.Release(this);
        spot = null;
    }

    // Outcomes before the routine: a scared or avoiding civilian is still Alerted, and the suspicion check
    // below would otherwise catch them and stop-and-look instead
    private void Update()
    {
        if (Activity == CivilianActivity.Scared)
        {
            if (Time.time >= calmTime) CalmDown();
            return;
        }

        if (Activity == CivilianActivity.Avoiding)
        {
            if (agent.HasArrived()) activity = ActivityFor(role);
            return;
        }

        if (suspicion.State >= NpcAlertState.Investigating)
        {
            StopAndLook();
            return;
        }

        if (stopped) Resume();

        if (spot != null && !spot.isActiveAndEnabled)
            ReleaseSpot();

        if (spot == null)
        {
            if (Time.time >= nextChooseTime) ChooseSpot();
            return;
        }

        if (!arrived)
        {
            if (agent.HasArrived())
            {
                arrived = true;
                leaveTime = Time.time + spot.RandomDwell();
            }
            return;
        }

        facing.LookDirection(spot.transform.forward);

        if (Time.time >= leaveTime)
        {
            ReleaseSpot();
            ChooseSpot();
        }
    }

    // After every Update, so a change the window made this frame is seen this frame, whichever component
    // Unity updated first
    private void LateUpdate()
    {
        CivilianActivity now = Activity;
        if (now == reported) return;

        CivilianActivity from = reported;
        reported = now;
        ActivityChanged?.Invoke(from, now);
    }

    private void CalmDown()
    {
        perception.SetViewScale(1f, 1f);
        activity = ActivityFor(role);
    }

    // While suspicious, and while the window runs: stop once, look at what caught the eye, keep the spot
    private void StopAndLook()
    {
        if (!stopped)
        {
            agent.ResetPath();
            stopped = true;
        }

        if (suspicion.HasStimulus)
            facing.LookAt(suspicion.LastStimulus);
    }

    private void Resume()
    {
        stopped = false;
        if (spot != null)
        {
            arrived = false;
            agent.SetDestination(spot.transform.position);
        }
    }

    private void ChooseSpot()
    {
        candidates.Clear();
        foreach (CivilianSpot s in CivilianSpot.All)
            if (s.Role == role && s.ClaimedBy == null)
                candidates.Add(s);

        if (candidates.Count == 0)
        {
            nextChooseTime = Time.time + retryDelay;
            return;
        }

        CivilianSpot next = candidates[Random.Range(0, candidates.Count)];
        next.TryClaim(this);

        spot = next;
        arrived = false;
        agent.SetDestination(spot.transform.position);
    }

    // The window filled on an outcome other than a call
    private void OnDecided(CivilianActivity outcome)
    {
        if (outcome == CivilianActivity.Scared)
        {
            // Stopped keeps the spot, so Resume walks them back to it once they calm down. Blind while scared:
            // no evidence for the witness, and suspicion decays naturally (thriftshop-level.md 2.4)
            agent.ResetPath();
            stopped = true;
            calmTime = Time.time + Random.Range(scaredSeconds.x, scaredSeconds.y);
            perception.SetViewScale(0f, 0f);
        }
        else if (outcome == CivilianActivity.Avoiding)
        {
            ReleaseSpot();
            stopped = false;
            agent.SetDestination(AwayFromStimulus());
        }
        // An outcome this role cannot carry out: keep the activity, rather than enter a state with no way out
        else return;

        activity = outcome;
    }

    // A point avoidDistance away from what alarmed them, snapped onto the NavMesh. With the player standing
    // on top of them the direction is near zero, so they back straight away instead
    private Vector3 AwayFromStimulus()
    {
        Vector3 away = suspicion.HasStimulus ? transform.position - suspicion.LastStimulus : -transform.forward;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;

        Vector3 target = transform.position + away.normalized * avoidDistance;
        return NavMesh.SamplePosition(target, out NavMeshHit hit, avoidDistance, agent.areaMask)
            ? hit.position : transform.position;
    }

    private void OnValidate()
    {
        if (scaredSeconds.x > scaredSeconds.y)
            Debug.LogWarning($"{name} scaredSeconds minimum is above its maximum", this);
    }

    private void OnDrawGizmos()
    {
        if (!debug) return;
#if UNITY_EDITOR
        // Activity reads the window, which is null in edit and prefab mode, where Awake has not run
        CivilianActivity shown = window != null ? Activity : activity;
        string label = shown.ToString();
        if (spot != null) label += $" → {spot.name}";
        if (stopped) label += " (stopped)";
        else if (arrived && spot != null) label += " (dwelling)";
        if (shown == CivilianActivity.Scared) label += $"  calms in {calmTime - Time.time:F1} s";

        // The reaction readout (C6): window is null in edit mode, where Awake has not run
        if (window != null && window.PendingLeaf != null)
        {
            string when = window.IsDeliberating ? $"deliberating {window.Progress:P0}" : "last decision";
            label += $"\n{when}: {window.PendingOutcome} ({window.PendingChance:P0}) at {window.PendingLeaf.name}";
            label += $"\n{window.PendingLeaf.DescribeScores()}";
            label += $"\n{window.Context}";
        }
        Handles.Label(transform.position + Vector3.up * 2.4f, label);
#endif
    }
}
