using UnityEngine;
using UnityEngine.AI;

// Poses an NPC from its NavMeshAgent. The agent owns position and GuardBehaviour owns rotation, so the
// Animator is visual only and root motion stays off. Speed is in metres per second, matching the
// thresholds NpcLocomotionBuilder measured from the clips. Shared by guards and, from M6, civilians.
[RequireComponent(typeof(NavMeshAgent))]
public class NpcAnimator : MonoBehaviour
{
    private static readonly int SpeedId = Animator.StringToHash("Speed");

    [SerializeField] private float dampTime = 0.1f;

    [SerializeField, Tooltip("Animator on the model. Found in children when left empty")]
    private Animator animator;

    [SerializeField, Range(0f, 0.5f), Tooltip("Seconds to settle on a new speed. Hides the agent's acceleration steps")]
    private float speedDampTime = 0.1f;

    private NavMeshAgent agent;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogWarning($"[NpcAnimator] No Animator under {name}; NpcAnimator disabled", this);
            enabled = false;
            return;
        }

        // Root motion would fight the agent for the transform: sliding at best, jitter at worst.
        animator.applyRootMotion = false;
    }

    private void Update()
    {
        // Actual velocity, not desiredVelocity: a guard blocked by a doorway should stop its legs too.
        Vector3 velocity = agent.velocity;
        velocity.y = 0f;

        animator.SetFloat(SpeedId, velocity.magnitude, speedDampTime, Time.deltaTime);
    }
}
