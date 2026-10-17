using System;
using System.Collections.Generic;

// The whole of security.json: one record per location, keyed by scene name
[Serializable]
public class SecuritySave
{
    public List<SecurityRecord> locations = new();

    // A handful of locations at most, so a plain scan; null when the location has no record yet
    public SecurityRecord Find(string location)
    {
        foreach (SecurityRecord record in locations)
            if (record.location == location) return record;
        return null;
    }
}
