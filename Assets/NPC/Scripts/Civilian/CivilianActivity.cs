// Decision tree outcomes from concept 6.8, plus the default behaviours they interrupt.
// Orthogonal to NpcAlertState: a civilian can be Alerted and Calling at once.
// PretendingNotToSee must look like Working to the player while it keeps observing.
// ReactionLeaf assets save outcomes as numbers, so add new values at the end only.
public enum CivilianActivity
{
    Roaming,
    Working,
    Deliberating,
    Scared,
    Calling,
    Avoiding,
    PretendingNotToSee,
    Dozing,
}
