// Parts of the player's body that NPC perception casts to (decision 13). Order is load-bearing and
// values stay implicit: the region bitmask on Observation and the per-region arrays on NpcPerception
// index by (int)region, so a new region goes at the end.
public enum BodyRegion
{
    Head,
    UpperBody,
    Hands,
    LowerBody,
    Feet
}