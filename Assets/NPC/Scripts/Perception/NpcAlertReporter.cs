using UnityEngine;

// Reports this NPC's alert state to JobAlert, so the HUD can show how alarmed the job is without knowing
// any NPC (dependencies point NPC to Core). Idle, Curious, Investigating and Alerted map one to one onto
// JobAlertLevel. On every NPC prefab
[RequireComponent(typeof(NpcSuspicion))]
public class NpcAlertReporter : MonoBehaviour
{
    private NpcSuspicion suspicion;
    private GuardBehaviour guard;
    private NpcDecisionWindow window;

    private void Awake()
    {
        suspicion = GetComponent<NpcSuspicion>();
        guard = GetComponent<GuardBehaviour>();
        window = GetComponent<NpcDecisionWindow>();
    }

    private void OnEnable()
    {
        suspicion.AlertStateChanged += OnAlertStateChanged;
        if (guard != null) guard.ActivityChanged += OnGuardActivityChanged;
        if (window != null) window.CallStarted += Report;
        Report();
    }

    private void OnDisable()
    {
        suspicion.AlertStateChanged -= OnAlertStateChanged;
        if (guard != null) guard.ActivityChanged -= OnGuardActivityChanged;
        if (window != null) window.CallStarted -= Report;
        JobAlert.Withdraw(this);
    }

    private void OnAlertStateChanged(NpcAlertState from, NpcAlertState to) => Report();
    private void OnGuardActivityChanged(GuardActivity from, GuardActivity to) => Report();

    private static JobAlertLevel ToLevel(NpcAlertState state) => state switch
    {
        NpcAlertState.Curious => JobAlertLevel.Suspicious,
        NpcAlertState.Investigating => JobAlertLevel.Searching,
        NpcAlertState.Alerted => JobAlertLevel.Alarm,
        _ => JobAlertLevel.Undetected,
    };

    private void Report()
    {
        JobAlertLevel level = ToLevel(suspicion.State);
        if (level > JobAlertLevel.Searching) level = JobAlertLevel.Searching;

        bool chasing = guard != null && guard.Activity == GuardActivity.Pursuing;
        bool calling = window != null && window.IsCalling;
        if (chasing || calling) level = JobAlertLevel.Alarm;

        JobAlert.Report(this, level);
    }
}
