using System;
using UnityEngine;

// SecurityCandidate describes one candidate POI and what it covers.
// It holds scene refrences not numbers, so reordering JobLayout can't mis-point
// it. SecurityDirector converts it to pure PoiCandidate at load.
[Serializable]
public struct SecurityCandidate
{
    [Tooltip("Inactive POI the plan may switch on")]
    public PointOfInterest poi;

    [Tooltip("Rooms this POI covers")]
    public Zone[] rooms;

    [Tooltip("Entrances this POI covers. At most one (solvability rule)")]
    public Entrance[] entrances;
}
