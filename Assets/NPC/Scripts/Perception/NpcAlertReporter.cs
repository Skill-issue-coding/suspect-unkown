using UnityEngine;

// Reports this NPC's alert state to JobAlert, so the HUD can show how alarmed the job is without knowing
// any NPC (dependencies point NPC to Core). Idle, Curious, Investigating and Alerted map one to one onto
// JobAlertLevel. On every NPC prefab
[RequireComponent(typeof(NpcSuspicion))]
public class NpcAlertReporter : MonoBehaviour
{
    private NpcSuspicion suspicion;

    private void Awake() => suspicion = GetComponent<NpcSuspicion>();

    private void OnEnable()
    {
        suspicion.AlertStateChanged += OnAlertStateChanged;
        JobAlert.Report(this, ToLevel(suspicion.State));
    }

    private void OnDisable()
    {
        suspicion.AlertStateChanged -= OnAlertStateChanged;
        JobAlert.Withdraw(this);
    }

    private void OnAlertStateChanged(NpcAlertState from, NpcAlertState to) => JobAlert.Report(this, ToLevel(to));

    private static JobAlertLevel ToLevel(NpcAlertState state) => state switch
    {
        NpcAlertState.Curious => JobAlertLevel.Suspicious,
        NpcAlertState.Investigating => JobAlertLevel.Searching,
        NpcAlertState.Alerted => JobAlertLevel.Alarm,
        _ => JobAlertLevel.Undetected,
    };
}
