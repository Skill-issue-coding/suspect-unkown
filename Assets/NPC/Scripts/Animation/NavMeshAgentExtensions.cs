using UnityEngine.AI;

// "Has the agent reached its destination?" in one place: every NPC behaviour asks it, and getting it
// subtly different in two places (forgetting pathPending) makes an NPC "arrive" the frame it starts
public static class NavMeshAgentExtensions
{
    public static bool HasArrived(this NavMeshAgent agent) =>
        !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;
}