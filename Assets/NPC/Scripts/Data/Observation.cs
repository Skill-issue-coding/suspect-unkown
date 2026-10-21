using UnityEngine;

// What a perceiver reports on a tick where it sees the player. The IPerceiver payload.
// Only exists when the player was seen; "not seen" is IPerceiver returning false, not a flag here.
// Quality factors stay separate, never pre-multiplied, so the debug readout can print each one
// and the witness (M7) can combine them with observerSkill and featureVisibility.
// Conduct and abnormality are judged by this perceiver's archetype.
// Per-region fractions are five plain floats, not an array, so copies never share them (decision 14).
public struct Observation
{
    public IPerceiver perceiver;
    public float timestamp;
    public Vector3 playerPosition;
    public float visibility;
    public float fDistance;
    public float fAngle;
    public float fLight;
    public float fStance;
    public ConductFlags conduct;
    public float abnormality;
    public CharacterAppearance appearance;
    public float recognition;

    // Share of a region's points with a clear line, 0 to 1. A region the archetype ignores casts no rays and stays 0
    private float headFraction;
    private float upperBodyFraction;
    private float handsFraction;
    private float lowerBodyFraction;
    private float feetFraction;

    public void SetRegionFraction(BodyRegion region, float fraction)
    {
        switch (region)
        {
            case BodyRegion.Head: headFraction = fraction; break;
            case BodyRegion.UpperBody: upperBodyFraction = fraction; break;
            case BodyRegion.Hands: handsFraction = fraction; break;
            case BodyRegion.LowerBody: lowerBodyFraction = fraction; break;
            case BodyRegion.Feet: feetFraction = fraction; break;
        }
    }

    public readonly float RegionFraction(BodyRegion region) => region switch
    {
        BodyRegion.Head => headFraction,
        BodyRegion.UpperBody => upperBodyFraction,
        BodyRegion.Hands => handsFraction,
        BodyRegion.LowerBody => lowerBodyFraction,
        BodyRegion.Feet => feetFraction,
        _ => 0f,
    };

    // Everything about the look except how much of the body was in view: the witness multiplies in the
    // slot's own region fraction instead of the whole-body visibility
    public readonly float ViewingConditions => fDistance * fAngle * fLight * fStance;

    public readonly float Strength => visibility * ViewingConditions; // a convenience for suspicion; the witness uses ViewingConditions with a region fraction
}
