using System;

// What a stolen item opens up for later jobs. Flags, so one item can unlock several things.
// Read by ProgressSave.Apply when a job succeeds, saved as numbers: never renumber a value
[Flags]
public enum ProgressUnlock
{
    None = 0,
    Shop = 1 << 0,
    ThriftShop = 1 << 1,
}
