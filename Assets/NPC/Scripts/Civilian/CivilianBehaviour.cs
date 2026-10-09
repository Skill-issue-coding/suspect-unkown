using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// A civilian's own life (decision 22): picks a free CivilianSpot for its role, walks there, stays a
// random time facing the spot's forward, and picks again. Customers roam, staff work. Until the decision
// window exists (M6), the only reaction is a placeholder: from Investigating up it stops and looks at
// the stimulus, and carries on when suspicion drops. Asks NpcFacing for a facing; never turns itself
[RequireComponent(typeof(NavMeshAgent), typeof(NpcSuspicion))]
[RequireComponent(typeof(NpcFacing))]
public class CivilianBehaviour : MonoBehaviour
{
    [Header("Role")]
    [SerializeField, Tooltip("Customer browses; Employee and Manager work. Also decides which spots it uses")]
    private CivilianRole role = CivilianRole.Customer;

    [Header("Routine")]
    [SerializeField, Tooltip("Metres per second between spots")]
    private float walkSpeed = 1.2f;

    [SerializeField, Tooltip("Seconds before trying again when no spot is free")]
    private float retryDelay = 2f;

    [Header("Debug")]
    [SerializeField] private bool debug = true;

    public CivilianRole Role => role;
    public CivilianActivity Activity { get; private set; }

    private readonly List<CivilianSpot> candidates = new();

    private NavMeshAgent agent;
    private NpcSuspicion suspicion;
    private NpcFacing facing;

    private CivilianSpot spot;
    private bool arrived;
    private bool stopped;
    private float leaveTime;
    private float nextChooseTime;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        suspicion = GetComponent<NpcSuspicion>();
        facing = GetComponent<NpcFacing>();

        agent.speed = walkSpeed;
        Activity = role == CivilianRole.Customer ? CivilianActivity.Roaming : CivilianActivity.Working;
    }

    private void OnDisable() => ReleaseSpot();

    private void ReleaseSpot()
    {
        if (spot != null) spot.Release(this);
        spot = null;
    }

    private void Update()
    {
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

    // Placeholder until the decision window (M6): stop once, look at what caught the eye, keep the spot
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

    private void OnDrawGizmos()
    {
        if (!debug) return;
#if UNITY_EDITOR
        string label = Activity.ToString();
        if (spot != null) label += $" → {spot.name}";
        if (stopped) label += " (stopped)";
        else if (arrived && spot != null) label += "(dwelling)";
        Handles.Label(transform.position + Vector3.up * 2.4f, label);
#endif
    }
}
