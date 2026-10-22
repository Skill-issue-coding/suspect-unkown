using System;

// What a stolen item opens up for later jobs. Flags, so one item can unlock several things.
// Read by ProgressSave.Apply when a job succeeds, saved as numbers: never renumber a value
[Flags]
public enum ProgressUnlock
{
    None = 0,

    // Gear
    Gear_Lockpick = 1 << 2,
    Gear_Lockpick2 = 1 << 3,
    Gear_Lockpick3 = 1 << 4,
    Gear_Hammer = 1 << 5,
    Gear_GlassBreaker = 1 << 6,
    Gear_GlassCutter = 1 << 7,
    Gear_SignalJammer = 1 << 15,
    Gear_CameraLooper = 1 << 16,

    // Skills
    Skill_SoftSteps = 1 << 8,
    Skill_Forgettable = 1 << 9,
    Skill_QuickHands = 1 << 10,
    Skill_Appraiser = 1 << 11,
    Skill_Negotiator = 1 << 12,
    // A new flag takes the next free bit, never one in the middle: saves and assets store these numbers
    Skill_BetterCutter = 1 << 17,

    // Abilities

    // Jobs
    Shop = 1 << 0,
    ThriftShop = 1 << 1,
    JewlryStore = 1 << 13,
    Bank = 1 << 14,
}
