using System;
using UnityEngine;

// One possible content of a LootChoice. Plain data, so its fields are public camelCase like Observation's
[Serializable]
public struct LootChoiceOption
{
    [Tooltip("What the spot holds if this option is picked: one loot pickup, or a group of them")]
    public GameObject content;

    [Min(0f), Tooltip("Relative chance against the other options. Read only in Weighted mode")]
    public float weight;
}
