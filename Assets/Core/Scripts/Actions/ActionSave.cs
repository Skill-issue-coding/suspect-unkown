using System;
using System.Collections.Generic;

// The whole of actions.json: one record per location, keyed by scene name
[Serializable]
public class ActionSave
{
    public List<ActionRecord> locations = new();

    // A handful of locations at most, so a plain scan; null when the location has no record yet
    public ActionRecord Find(string location)
    {
        foreach (ActionRecord record in locations)
            if (record.location == location) return record;
        return null;
    }
}
