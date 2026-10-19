using UnityEngine;

// What one kind of civilian does when its decision window fills (thriftshop-level.md 2.4), referenced
// by the archetype so a role's reactions are data. The one entry point: walks from the root to a leaf
// for the given context and samples that leaf. NpcDecisionWindow calls it once, when the window opens
[CreateAssetMenu(fileName = "ReactionTree", menuName = "Suspect Unknown/NPC/Reaction/Tree")]
public class ReactionTree : ScriptableObject
{
    [SerializeField, Tooltip("A branch, or one leaf for a civilian who always reacts the same way")]
    private ReactionNode root;

    public bool TrySample(in ReactionContext context, out ReactionLeaf leaf, out CivilianActivity outcome, out float chance)
    {
        leaf = root != null ? root.Resolve(context, 0) : null;
        if (leaf != null) return leaf.TrySample(out outcome, out chance);

        Debug.LogWarning($"{name} reached no leaf for {context}", this);
        outcome = default;
        chance = 0f;
        return false;
    }

    private void OnValidate()
    {
        if (root == null)
            Debug.LogWarning($"{name} has no root node, cannot reach leaves", this);
    }
}
