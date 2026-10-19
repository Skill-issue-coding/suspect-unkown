// What a civilian just did, as the HUD's reaction toast announces it. Mirrors the reaction outcomes of
// CivilianActivity, which Core cannot see, as JobAlertLevel mirrors NpcAlertState, plus the two moments
// around a call the player can still act on: a window leaning toward a call opens (AboutToCall) and it
// drains without one (LostSight). Nothing relies on the order
public enum JobReaction
{
    Scared,
    CalmedDown,
    Avoiding,
    Calling,
    AboutToCall,
    LostSight,
}
