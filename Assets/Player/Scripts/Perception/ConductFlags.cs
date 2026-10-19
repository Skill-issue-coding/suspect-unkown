using System;

// What the player is doing, as a set: NPCs judge each flag as 
// abnormal or not (decision 6). Values must be powers of two 
// (1 << n). Add a flag only together with the code that sets it.
[Flags]
public enum ConductFlags
{
    None = 0,
    Sprinting = 1 << 0,
    InStaffArea = 1 << 1,
    Lockpicking = 1 << 2,
    Stealing = 1 << 3,
    GlassCutting = 1 << 4,
}
