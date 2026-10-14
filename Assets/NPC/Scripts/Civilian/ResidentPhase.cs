// Where the resident is in their sleep rhythm (apartment-level.md 2.2). Separate from CivilianActivity,
// which is what other systems read: Dozing maps to Dozing, Stirring and Awake to Working. The snack run
// adds its phases in B5. Nothing relies on the declaration order
public enum ResidentPhase
{
    Dozing,
    Stirring,
    Awake,
    GettingUp,
    ToKitchen,
    Snacking,
    Returning,
}
