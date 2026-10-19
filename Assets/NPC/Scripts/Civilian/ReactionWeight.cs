using System;
using UnityEngine;

// One outcome a ReactionLeaf can pick and how likely it is relative to the leaf's other outcomes.
// A plain data pair, so its fields are public, as in ConductWeights
[Serializable]
public struct ReactionWeight
{
    [Tooltip("Calling, Scared or Avoiding: the outcomes a civilian can carry out. Anything else counts as 0")]
    public CivilianActivity outcome;

    [Min(0f), Tooltip("Chance relative to the leaf's other outcomes. 0 is never picked")]
    public float weight;
}
