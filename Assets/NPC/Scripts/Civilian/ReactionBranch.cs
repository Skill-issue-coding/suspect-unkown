using UnityEngine;

// A coarse split in a reaction tree: tests one input of the ReactionContext against a threshold and
// passes the context on to one of two children. Branches carry the "who is this and how bad was it"
// logic, so leaves only have to weigh outcomes
[CreateAssetMenu(fileName = "ReactionBranch", menuName = "Suspect Unknown/NPC/Reaction/Branch")]
public class ReactionBranch : ReactionNode
{
    // Deeper than any hand-made tree should be; reaching it means a child points back up the tree
    private const int MaxDepth = 16;

    [SerializeField, Tooltip("Which input of the context this branch tests")]
    private ReactionInput input;

    [SerializeField, Tooltip("Below this value the context goes to Below, at or above it to Above. Severity is 0 to 1, distance in metres")]
    private float threshold;

    [SerializeField, Tooltip("The node for an input below the threshold")]
    private ReactionNode below;

    [SerializeField, Tooltip("The node for an input at or above the threshold")]
    private ReactionNode above;

    public override ReactionLeaf Resolve(in ReactionContext context, int depth)
    {
        if (depth >= MaxDepth)
        {
            Debug.LogWarning($"{name} is {MaxDepth} branches deep; the tree probably loops", this);
            return null;
        }
        ReactionNode next = context.Get(input) < threshold ? below : above;
        return next != null ? next.Resolve(context, depth + 1) : null;
    }

    private void OnValidate()
    {
        if (below == null)
            Debug.LogWarning($"{name} has no Below node; a context below the threshold reaches no leaf", this);

        if (above == null)
            Debug.LogWarning($"{name} has no Above node; a context at or above the threshold reaches no leaf", this);

        if (below == this || above == this)
            Debug.LogWarning($"{name} points to itself; the tree loops", this);
    }
}
