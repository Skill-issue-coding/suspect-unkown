// Decision tree outcomes from concept 6.8, plus the default behaviours they interrupt.
// Orthogonal to NpcAlertState: a civilian can be Alerted and Calling at once.
public enum CivilianActivity
{
    Roaming,
    Working,
    Deliberating,
    Frozen,
    Calling,
    Fleeing,
    Filming,
    Confronting,
    Avoiding
}
