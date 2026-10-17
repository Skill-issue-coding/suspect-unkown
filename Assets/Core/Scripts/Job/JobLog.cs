using System;
using UnityEngine;

// What the player did in this run, for adaptive security (thriftshop-level.md 4.2): room transitions and
// inbound entrance crossings, kept whether or not anyone saw them (decision 23). Counts only; the save
// decays and adds them on completion (S2). Sized by the level's JobLayout: Begin in its Awake, End when it
// goes away, so a level without a layout records nothing. Transitions are one flat array
// (from × rooms + to) because JsonUtility serializes neither 2D arrays nor dictionaries
public static class JobLog
{
    private static int[] transitions = Array.Empty<int>();
    private static int[] entries = Array.Empty<int>();

    public static bool IsActive { get; private set; }
    public static int RoomCount { get; private set; }
    public static int EntranceCount => entries.Length;

    // Raised after every recorded change and on Begin, for the readout
    public static event Action Recorded;

    private static object owner;

    public static void Begin(object source, int roomCount, int entranceCount)
    {
        owner = source;
        RoomCount = roomCount;
        transitions = new int[roomCount * roomCount];
        entries = new int[entranceCount];
        IsActive = true;
        Recorded?.Invoke();
    }

    // Only the layout that began this log may end it: on an async scene load the old layout can be
    // destroyed after the new one has already begun
    public static void End(object source)
    {
        if (source != owner) return;
        Clear();
    }

    private static void Clear()
    {
        owner = null;
        IsActive = false;
        RoomCount = 0;
        transitions = Array.Empty<int>();
        entries = Array.Empty<int>();
    }

    public static void RecordRoomChange(int from, int to)
    {
        if (!IsActive || from == to) return;
        if ((uint)from >= (uint)RoomCount || (uint)to >= (uint)RoomCount) return;

        transitions[from * RoomCount + to]++;
        Recorded?.Invoke();
    }

    public static void RecordEntry(int entrance)
    {
        if (!IsActive) return;
        if ((uint)entrance >= (uint)EntranceCount) return;

        entries[entrance]++;
        Recorded?.Invoke();
    }

    // Unchecked: callers loop below RoomCount and EntranceCount, as the readout and the save do
    public static int Transitions(int from, int to) => transitions[from * RoomCount + to];

    public static int Entries(int entrance) => entries[entrance];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => Clear();
}
