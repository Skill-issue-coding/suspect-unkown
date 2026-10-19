using UnityEngine;

// Announces a guard's steps to JobAlert for the HUD's toast, so Core learns what a guard is doing without
// knowing GuardBehaviour: noticing something (Watching), investigating, chasing, losing the player, giving
// up. A glance that ends without investigating (Watching back to Patrolling) announces nothing, so the
// ordinary patrol does not fill the toast. On Guard.prefab; the civilians' counterpart is NpcReactionReporter
[RequireComponent(typeof(GuardBehaviour))]
public class GuardReactionReporter : MonoBehaviour
{
    private GuardBehaviour guard;

    private void Awake() => guard = GetComponent<GuardBehaviour>();

    private void OnEnable() => guard.ActivityChanged += OnActivityChanged;

    private void OnDisable() => guard.ActivityChanged -= OnActivityChanged;

    private void OnActivityChanged(GuardActivity from, GuardActivity to)
    {
        switch (to)
        {
            case GuardActivity.Watching:
                JobAlert.ReportReaction(JobReaction.GuardCurious);
                break;
            case GuardActivity.Investigating:
                JobAlert.ReportReaction(from == GuardActivity.Pursuing ? JobReaction.GuardLostYou : JobReaction.GuardInvestigating);
                break;
            case GuardActivity.Pursuing:
                JobAlert.ReportReaction(JobReaction.GuardChasing);
                break;
            case GuardActivity.Patrolling when from is GuardActivity.Investigating or GuardActivity.Pursuing:
                JobAlert.ReportReaction(JobReaction.GuardGaveUp);
                break;
        }
    }
}
