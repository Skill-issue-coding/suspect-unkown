// What a ReactionBranch can test, read from the ReactionContext. Only inputs something produces today.
// Branch assets save the enum as a number, so add new values at the end: a value inserted in the
// middle changes what existing branches test
public enum ReactionInput
{
    Severity,
    PlayerDistance,
}
