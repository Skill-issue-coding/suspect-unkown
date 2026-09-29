using System;
using UnityEngine;
using UnityEngine.AI;

// Guard behaviour. Serves points of interest while idle (decision 15): 
// choose the neediest reachable, unclaimed POI by need × importance 
// discounted by path length, walk there, dwell until covered, repeat. 
// Reports presence to every POI it stands in. Alert state drives the 
// activity each frame, so every reaction is interruptible; giving up is 
// Idle, which patrols again.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NpcSuspicion))]
[RequireComponent(typeof(NpcPerception))]
public class GuardBehaviour : MonoBehaviour
{
    // Below 0.25 m/s the guard keeps its facing: steering is noisy while starting and braking.
    private const float MinFacingSpeedSqr = 0.25f * 0.25f;

    [Header("Patrol")]
    [SerializeField, Tooltip("Metres per second while serving points of interest")]
    private float patrolSpeed = 1.5f;

    [SerializeField, Tooltip("Metres of walking that halve a POI's score. Higher means the guard minds distance less")]
    private float distanceScale = 10f;

    [SerializeField, Range(0f, 1f), Tooltip("Coverage at which a dwelling guard considers the POI done")]
    private float fullCoverage = 0.95f;

    [SerializeField, Tooltip("Upper bound on dwelling, in case a POI fills slower than expected")]
    private float maxDwellTime = 10f;

    [SerializeField, Tooltip("Seconds between retries when no POI needs anyone")]
    private float retryInterval = 0.5f;

    [Header("Alert")]
    [SerializeField, Tooltip("Maximum degrees per second the guard turns")]
    private float turnSpeed = 240f;

    [SerializeField, Range(0f, 2f), Tooltip("Seconds to catch up with a new facing. Higher is lazier")]
    private float turnSmoothTime = 0.2f;

    [SerializeField, Tooltip("Metres per second while walking to the last-known position")]
    private float investigateSpeed = 2.5f;

    [SerializeField, Range(1f, 6f), Tooltip("Keep below the player's sprint (6) so escape is possible")]
    private float pursueSpeed = 4.5f;

    [SerializeField, Range(0.1f, 2f), Tooltip("Only re-path when the last-known position moves this far")]
    private float repathDistance = 0.75f;

    [SerializeField, Range(0.1f, 3f), Tooltip("Horizontal metres at which a pursuing guard catches a visible player")]
    private float catchRadius = 1.2f;

    [SerializeField, Tooltip("Degrees left and right the guard sweeps while searching")]
    private float searchSweepAngle = 66f;

    [SerializeField, Range(1f, 5f), Tooltip("How fast the search sweep swings left and right")]
    private float searchSweepSpeed = 1.2f;

    [SerializeField, Tooltip("Seconds the guard stands still after giving up, for the give-up clip")]
    private float giveUpPause = 3f;

    [SerializeField, Range(0f, 1f), Tooltip("Seconds without a sighting before the guard counts the player as lost")]
    private float lostSightGrace = 0.3f;

    [SerializeField, Tooltip("Optional. A guard without a rig simply doesn't turn its head")]
    private NpcHeadLook headLook;

    [Header("Debug")]
    [SerializeField] private bool debug = true;

    public event Action<GuardActivity, GuardActivity> ActivityChanged;
    public event Action Caught;

    public GuardActivity Activity { get; private set; } = GuardActivity.Patrolling;

    public bool IsSearching => arrived &&
        (Activity == GuardActivity.Investigating || (Activity == GuardActivity.Pursuing && LostSight));

    public bool IsDwelling => Activity == GuardActivity.Patrolling && target != null && arrived;

    private NavMeshAgent agent;
    private NavMeshPath path;

    private PointOfInterest target;
    private bool arrived;
    private float dwellTimer;
    private float nextChooseTime;

    private NpcSuspicion suspicion;
    private NpcPerception perception;

    private bool seesPlayer;
    private bool hasLastKnown;
    private Vector3 lastKnownPosition;

    private float desiredYaw;
    private bool hasDesiredYaw;
    private float yawVelocity;
    private float searchBaseYaw;

    private float searchTimer;
    private bool caught;

    private float lastSeenTime = float.NegativeInfinity;

    private bool LostSight => Time.time - lastSeenTime > lostSightGrace;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();
        agent.updateRotation = false;

        // Guards start in Patrolling without an activity change, so its speed is set here once;
        // SetActivity sets it on every later return to patrol.
        agent.speed = patrolSpeed;

        suspicion = GetComponent<NpcSuspicion>();
        perception = GetComponent<NpcPerception>();
    }

    private void OnDisable()
    {
        if (target != null) target.Release(this);
        target = null;
    }

    private void Update()
    {
        if (caught) return;

        OccupyNearby();
        TrackPlayer();
        SetActivity(ActivityFor(suspicion.State));

        switch (Activity)
        {
            case GuardActivity.Patrolling: UpdatePatrol(); break;
            case GuardActivity.Watching: UpdateWatching(); break;
            case GuardActivity.Investigating: UpdateInvestigating(); break;
            case GuardActivity.Pursuing: UpdatePursuing(); break;
        }

        UpdateHead();
        UpdateRotation();
    }

    private void LookAt(Vector3 point) => LookDirection(point - transform.position);

    private void LookDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        desiredYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        hasDesiredYaw = true;
    }

    private void UpdateRotation()
    {
        if (!hasDesiredYaw && agent.desiredVelocity.sqrMagnitude > MinFacingSpeedSqr)
            LookDirection(agent.desiredVelocity);

        if (hasDesiredYaw)
        {
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, desiredYaw, ref yawVelocity, turnSmoothTime, turnSpeed);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        hasDesiredYaw = false;
    }

    private static GuardActivity ActivityFor(NpcAlertState state) => state switch
    {
        NpcAlertState.Idle => GuardActivity.Patrolling,
        NpcAlertState.Curious => GuardActivity.Watching,
        NpcAlertState.Investigating => GuardActivity.Investigating,
        NpcAlertState.Alerted => GuardActivity.Pursuing,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    private void TrackPlayer()
    {
        seesPlayer = perception.TryGetLatest(out Observation observation);
        if (!seesPlayer) return;

        lastSeenTime = Time.time;

        float threshold = Activity == GuardActivity.Pursuing ? 0.1f : repathDistance;
        bool moved = !hasLastKnown
            || (observation.playerPosition - lastKnownPosition).sqrMagnitude > threshold * threshold;

        lastKnownPosition = observation.playerPosition;
        hasLastKnown = true;

        if (moved && (Activity == GuardActivity.Investigating || Activity == GuardActivity.Pursuing))
            MoveTo(lastKnownPosition);
    }

    private void SetActivity(GuardActivity next)
    {
        if (next == Activity) return;

        GuardActivity from = Activity;

        if (from == GuardActivity.Patrolling && target != null)
        {
            target.Release(this);
            target = null;
        }

        Activity = next;

        agent.autoBraking = true;

        switch (next)
        {
            case GuardActivity.Patrolling:
                agent.speed = patrolSpeed;
                bool gaveUp = from == GuardActivity.Investigating || from == GuardActivity.Pursuing;
                nextChooseTime = gaveUp ? Time.time + giveUpPause : 0f;
                if (gaveUp) agent.ResetPath();
                break;
            case GuardActivity.Watching: agent.ResetPath(); break;
            case GuardActivity.Investigating: agent.speed = investigateSpeed; MoveTo(lastKnownPosition); break;
            case GuardActivity.Pursuing: agent.speed = pursueSpeed; MoveTo(lastKnownPosition); break;
        }

        ActivityChanged?.Invoke(from, next);
    }

    private void MoveTo(Vector3 position)
    {
        agent.SetDestination(position);
        arrived = false;
        searchTimer = 0f;
    }

    private void OccupyNearby()
    {
        foreach (PointOfInterest poi in PointOfInterest.All)
            if (poi.Contains(transform.position))
                poi.Occupy();
    }

    private void UpdateWatching() { if (hasLastKnown) LookAt(lastKnownPosition); }

    private void UpdateInvestigating()
    {
        if (!arrived)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                arrived = true;
                searchBaseYaw = transform.eulerAngles.y;
            }

            return;
        }

        Search();
    }

    private void Search()
    {
        searchTimer += Time.deltaTime;
        desiredYaw = searchBaseYaw + Mathf.Sin(searchTimer * searchSweepSpeed) * searchSweepAngle;
        hasDesiredYaw = true;
    }

    private void UpdatePursuing()
    {
        bool chasing = !LostSight;
        agent.autoBraking = !chasing;

        if (chasing)
        {
            if (seesPlayer)
            {
                Vector3 offset = lastKnownPosition - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= catchRadius * catchRadius)
                    Catch();
            }
            return;
        }

        UpdateInvestigating();
    }

    private void Catch()
    {
        caught = true;
        agent.ResetPath();
        Caught?.Invoke();
    }

    private void UpdatePatrol()
    {
        if (target != null && !target.isActiveAndEnabled)
            target = null;

        if (target == null)
        {
            if (Time.time >= nextChooseTime)
                ChooseTarget();
            return;
        }

        if (!arrived)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                arrived = true;
                dwellTimer = 0f;
            }
            return;
        }

        dwellTimer += Time.deltaTime;
        LookDirection(target.transform.forward);

        bool dwelledEnough = dwellTimer >= target.DwellTime;
        bool covered = target.Coverage >= fullCoverage;

        if ((dwelledEnough && covered) || dwellTimer >= maxDwellTime)
        {
            target.Release(this);
            target = null;
        }
    }

    private void ChooseTarget()
    {
        PointOfInterest best = null;
        float bestScore = 0f;

        foreach (PointOfInterest poi in PointOfInterest.All)
        {
            if (poi.ClaimedBy != null && poi.ClaimedBy != this) continue;

            float need = 1f - poi.Coverage;
            if (need <= 0f) continue;

            float distance = PathLength(poi.transform.position);
            if (float.IsPositiveInfinity(distance)) continue;

            float score = need * poi.Importance / (1f + distance / distanceScale);
            if (score > bestScore)
            {
                bestScore = score;
                best = poi;
            }
        }

        if (best == null || !best.TryClaim(this))
        {
            nextChooseTime = Time.time + retryInterval;
            return;
        }

        target = best;
        arrived = false;
        agent.SetDestination(target.transform.position);
    }

    private float PathLength(Vector3 destination)
    {
        if (!NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path)
            || path.status != NavMeshPathStatus.PathComplete)
            return float.PositiveInfinity;

        float length = 0f;
        Vector3[] corners = path.corners;
        for (int i = 1; i < corners.Length; i++)
            length += Vector3.Distance(corners[i - 1], corners[i]);

        return length;
    }

    // Head on the last-known spot while alert and not searching. While searching, the clip and the
    // sweep do the looking; while patrolling, the animation's own head motion plays.
    private void UpdateHead()
    {
        if (headLook == null) return;

        bool alert = Activity == GuardActivity.Watching
            || Activity == GuardActivity.Investigating
            || Activity == GuardActivity.Pursuing;

        if (hasLastKnown && alert && !IsSearching)
            headLook.LookAt(lastKnownPosition);
        else
            headLook.ClearLook();
    }

    private void OnDrawGizmos()
    {
        if (!debug) return;

#if UNITY_EDITOR
        string label = Activity.ToString();
        if (target != null) label += $" → {target.name}";
        if (IsSearching) label += " (searching)";
        if (IsDwelling) label += " (dwelling)";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.4f, label);
#endif

        if (target != null)
        {
            Gizmos.color = arrived ? Color.green : Color.yellow;
            Gizmos.DrawLine(transform.position + Vector3.up, target.transform.position + Vector3.up);
        }

        if (hasLastKnown && Activity != GuardActivity.Patrolling)
        {
            Gizmos.color = Activity == GuardActivity.Pursuing ? Color.red : new Color(1f, 0.55f, 0f);
            Gizmos.DrawWireSphere(lastKnownPosition, 0.3f);
            Gizmos.DrawLine(transform.position + Vector3.up, lastKnownPosition);
        }
    }
}
