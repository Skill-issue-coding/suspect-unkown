// What an NPC just did, as the HUD's reaction toast announces it. Core cannot see the NPC types, so this
// mirrors them as JobAlertLevel mirrors NpcAlertState: the civilian outcomes of CivilianActivity, the two
// moments around a call the player can still act on (a window leaning toward a call opens, AboutToCall,
// and drains without one, LostSight), and the guard's steps of GuardActivity. Nothing relies on the order
public enum JobReaction
{
    Scared,
    CalmedDown,
    Avoiding,
    Calling,
    AboutToCall,
    LostSight,
    GuardCurious,
    GuardInvestigating,
    GuardChasing,
    GuardLostYou,
    GuardGaveUp,
}
