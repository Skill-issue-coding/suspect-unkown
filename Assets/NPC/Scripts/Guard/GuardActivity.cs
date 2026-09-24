// What a guard is doing. Patrolling serves points of interest (decision 15); Watching is Curious:
// stopped, facing the last-known position. Reporting waits for M8's monitor.
public enum GuardActivity
{
    Patrolling,
    Watching,
    Investigating,
    Pursuing,
    Reporting
}
