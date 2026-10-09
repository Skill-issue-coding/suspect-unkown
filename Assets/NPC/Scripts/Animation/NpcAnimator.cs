using UnityEngine;
using UnityEngine.AI;

// Poses an NPC from its NavMeshAgent. The agent owns position and NpcFacing owns rotation, so the
// Animator is visual only and root motion stays off. Speed is in metres per second, matching the
// thresholds the locomotion builder measured from the clips. TurnRate is read in LateUpdate, after
// NpcFacing has turned the body this frame. Shared by guards and, from M6, civilians.
[RequireComponent(typeof(NavMeshAgent))]
public class NpcAnimator : MonoBehaviour
{
    [SerializeField, Tooltip("Animator on the model. Found in children when left empty")]
    private Animator animator;

    [SerializeField, Range(0f, 0.5f), Tooltip("Seconds to settle on a new speed. Hides the agent's acceleration steps")]
    private float speedDampTime = 0.1f;

    [SerializeField, Range(0f, 0.5f), Tooltip("Seconds to settle on a new turn rate. Hides frame-to-frame noise")]
    private float turnDampTime = 0.15f;

    private NavMeshAgent agent;
    private float lastYaw;

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

        lastYaw = transform.eulerAngles.y;
    }

    private void Update()
    {
        // Actual velocity, not desiredVelocity: a guard blocked by a doorway should stop its legs too.
        Vector3 velocity = agent.velocity;
        velocity.y = 0f;

        animator.SetFloat(NpcAnimatorParams.SpeedId, velocity.magnitude, speedDampTime, Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f) return;

        float yaw = transform.eulerAngles.y;
        float turnRate = Mathf.DeltaAngle(lastYaw, yaw) / Time.deltaTime;
        lastYaw = yaw;

        animator.SetFloat(NpcAnimatorParams.TurnRateId, turnRate, turnDampTime, Time.deltaTime);
    }
}
