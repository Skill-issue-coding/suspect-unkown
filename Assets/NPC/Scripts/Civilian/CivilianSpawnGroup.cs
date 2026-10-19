using System;
using UnityEngine;

// One group of civilians a level spawns at load (thriftshop-level.md Track C): a prefab, the role every
// member gets, an optional archetype, how many there are, and the points they may start at. A fixed
// member, like the clerk at the register, is a group with a count of 1 to 1. The archetype lets one prefab
// serve every role, since a role alone does not change how an NPC perceives. Plain data, so
// CivilianSpawner does the work
[Serializable]
public struct CivilianSpawnGroup
{
    [Tooltip("Civilian prefab spawned for each member")]
    public CivilianBehaviour prefab;

    [Tooltip("Role given to every member after it spawns, whatever the prefab says. Decides which CivilianSpots it uses")]
    public CivilianRole role;

    [Tooltip("Optional. Perception tuning given to every member, replacing the prefab's. The clerk uses TiredClerk. Empty: keep the prefab's")]
    public NpcArchetype archetype;

    [Tooltip("Fewest and most spawned per run, both included. 1 and 1 for a fixed member")]
    public Vector2Int count;

    [Tooltip("Where this group may start, on the NavMesh. Each point is used once")]
    public Transform[] points;
}
