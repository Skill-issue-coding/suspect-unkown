// Suspicion ladder from concept 6.1. Reporting is an activity, not a level, so it is not here.
// Cameras have no ladder of their own; a guard watching the feed does.
public enum NpcAlertState
{
    Idle,
    Curious,
    Investigating,
    Alerted
}
