using UnityEngine;

// Announces this NPC's reactions to JobAlert for the HUD's toast, so Core learns what happened without
// knowing any NPC type. The call comes from the decision window's CallStarted, so it is announced once;
// the other reactions come from the civilian's activity (ICivilianActivity), which the resident reports
// too but never reaches, since it has no tree
[RequireComponent(typeof(NpcDecisionWindow))]
public class NpcReactionReporter : MonoBehaviour
{
    private NpcDecisionWindow window;
    private ICivilianActivity civilian;

    private void Awake()
    {
        window = GetComponent<NpcDecisionWindow>();
        civilian = GetComponent<ICivilianActivity>();
    }

    private void OnEnable()
    {
        window.CallStarted += OnCallStarted;
        if (civilian != null)
            civilian.ActivityChanged += OnActivityChanged;
    }

    private void OnDisable()
    {
        window.CallStarted -= OnCallStarted;
        if (civilian != null)
            civilian.ActivityChanged -= OnActivityChanged;
    }

    // PendingOutcome is set when the window opens and kept after it closes, so it says what the window
    // leaned toward both when Deliberating starts and when it ends. Scared to anything is calming down: a
    // scared civilian is blind, so it can only leave Scared by CalmDown
    private void OnActivityChanged(CivilianActivity from, CivilianActivity to)
    {
        bool leanedToCall = window.PendingOutcome == CivilianActivity.Calling;

        // The warning, while hiding still drains the window
        if (to == CivilianActivity.Deliberating && leanedToCall) JobAlert.ReportReaction(JobReaction.AboutToCall);
        // The window drained without a call: the player got out of sight in time. A call is announced by
        // CallStarted instead
        else if (from == CivilianActivity.Deliberating && to != CivilianActivity.Calling && leanedToCall)
            JobAlert.ReportReaction(JobReaction.LostSight);
        else if (to == CivilianActivity.Scared) JobAlert.ReportReaction(JobReaction.Scared);
        else if (to == CivilianActivity.Avoiding) JobAlert.ReportReaction(JobReaction.Avoiding);
        else if (from == CivilianActivity.Scared) JobAlert.ReportReaction(JobReaction.CalmedDown);
    }

    private void OnCallStarted() => JobAlert.ReportReaction(JobReaction.Calling);
}
