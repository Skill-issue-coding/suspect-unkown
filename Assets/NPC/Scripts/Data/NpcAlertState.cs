// Suspicion ladder from concept 6.1. Reporting is an activity, not a level, so it is not here.
// Cameras have no ladder of their own; a guard watching the feed does.
// Order is load-bearing: NpcSuspicion climbs and falls with enum arithmetic, so a new state goes
// where it belongs on the ladder, and the thresholds in NpcArchetype.Threshold follow.
public enum NpcAlertState
{
    Idle,
    Curious,
    Investigating,
    Alerted
}
