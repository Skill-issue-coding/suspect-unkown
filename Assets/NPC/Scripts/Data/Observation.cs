using UnityEngine;

// What a perceiver reports on a tick where it sees the player. The IPerceiver payload.
// Only exists when the player was seen; "not seen" is IPerceiver returning false, not a flag here.
// Quality factors stay separate, never pre-multiplied, so the debug readout can print each one
// and the witness (M7) can combine them with observerSkill and featureVisibility.
// Conduct and abnormality are judged by this perceiver's archetype.
public struct Observation
{
    public IPerceiver perceiver;
    public float timestamp;
    public Vector3 playerPosition;
    public float visibility;
    public int visibleRegions; // Bit on when any point in the region had a clear line. Index is (int)region, so giving BodyRegion explicit values breaks it
    public float fDistance;
    public float fAngle;
    public float fLight;
    public float fStance;
    public ConductFlags conduct;
    public float abnormality;
    public CharacterAppearance appearance;

    public void SetRegionVisible(BodyRegion region, bool visible)
    {
        int mask = 1 << (int)region;
        if (visible)
            visibleRegions |= mask;
        else
            visibleRegions &= ~mask;
    }

    public readonly bool IsRegionVisible(BodyRegion region)
    {
        int mask = 1 << (int)region;
        return (visibleRegions & mask) != 0;
    }
    public readonly float Strength => visibility * fDistance * fAngle * fLight * fStance; // this is a convenience for suspicion, and the witness quality formula (M7) uses the factors directly.
}
