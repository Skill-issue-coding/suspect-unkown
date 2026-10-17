using System;

// What security remembers about one location (thriftshop-level.md 4.3): the player's room transitions and
// entrance crossings over completed runs, decayed so recent runs dominate. Plain data for JsonUtility, with
// no NPC types, so the main menu can read it. The room and entrance names are kept so a changed layout is
// noticed: counts saved against another room list would be read into the wrong rooms
[Serializable]
public class ActionRecord
{
    public string location;
    public int completedRuns;
    public string[] rooms = Array.Empty<string>();
    public string[] entrances = Array.Empty<string>();
    public float[] transitions = Array.Empty<float>();   // rooms × rooms, from × rooms + to
    public float[] entries = Array.Empty<float>();

    // An empty record shaped by the level's layout: its names, and zeroed counts of the right size
    public static ActionRecord For(string location, JobLayout layout)
    {
        var record = new ActionRecord
        {
            location = location,
            rooms = new string[layout.RoomCount],
            entrances = new string[layout.EntranceCount],
            transitions = new float[layout.RoomCount * layout.RoomCount],
            entries = new float[layout.EntranceCount],
        };

        for (int i = 0; i < record.rooms.Length; i++)
            record.rooms[i] = layout.RoomName(i);
        for (int i = 0; i < record.entrances.Length; i++)
            record.entrances[i] = layout.EntranceName(i);

        return record;
    }

    public bool Fits(JobLayout layout)
    {
        if (rooms.Length != layout.RoomCount || entrances.Length != layout.EntranceCount) return false;
        for (int i = 0; i < rooms.Length; i++)
            if (rooms[i] != layout.RoomName(i)) return false;
        for (int i = 0; i < entrances.Length; i++)
            if (entrances[i] != layout.EntranceName(i)) return false;
        return true;
    }

    // C ← decay · C + this run, from JobLog. Only after Fits(layout): JobLog is sized by the same layout,
    // so every index below is in range for both
    public void AddRun(float decay)
    {
        int n = rooms.Length;
        for (int from = 0; from < n; from++)
            for (int to = 0; to < n; to++)
                transitions[from * n + to] = decay * transitions[from * n + to] + JobLog.Transitions(from, to);

        for (int e = 0; e < entries.Length; e++)
            entries[e] = decay * entries[e] + JobLog.Entries(e);

        completedRuns++;
    }
}
