using UnityEngine;

// One node of a civilian's reaction tree (thriftshop-level.md 2.4): a ReactionBranch that tests one
// input, or a ReactionLeaf that scores the outcomes. Assets, so a role's reactions are data and not
// code. Shared by every civilian using the tree, so it holds no runtime state (decision 1)
public abstract class ReactionNode : ScriptableObject
{
    // The leaf this context ends at, or null when a branch is missing a child. Depth counts the branches
    // passed, so a tree that loops back on itself stops instead of overflowing the stack
    public abstract ReactionLeaf Resolve(in ReactionContext context, int depth);
}
