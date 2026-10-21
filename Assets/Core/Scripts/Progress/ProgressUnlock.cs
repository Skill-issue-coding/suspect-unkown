using System;

// What a stolen item opens up for later jobs. Flags, so one item can unlock several things.
// Read by ProgressSave.Apply when a job succeeds, saved as numbers: never renumber a value
[Flags]
public enum ProgressUnlock
{
    None = 0,
    Shop = 1 << 0,
    ThriftShop = 1 << 1,

    // Gear
    Gear_Lockpick = 1 << 2,
    Gear_Hammer = 1 << 3,
    Gear_GlassBreaker = 1 << 4,
    Gear_GlassCutter = 1 << 5,

    // Skills
    Skill_Lockpick1 = 1 << 6,
    Skill_Lockpick2 = 1 << 7,
    Skill_Lockpick3 = 1 << 8,
    Skill_Bargaining = 1 << 9,

    // Abilities (Silent Footsteps)
    Ability_SilentCrouch = 1 << 10,
    Ability_SilentWalk = 1 << 11,
    Ability_SilentRun = 1 << 12,
}
