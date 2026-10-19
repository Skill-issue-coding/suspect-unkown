using UnityEngine;
using UnityEngine.AI;

// Poses an NPC from its NavMeshAgent. The agent owns position and NpcFacing owns rotation, so the
// Animator is visual only and root motion stays off. Speed is in metres per second, matching the
// thresholds the locomotion builder measured from the clips. TurnRate is read in LateUpdate, after
// NpcFacing has turned the body this frame. While crossing a NavMesh link the speed comes from the
// body's movement instead, since the agent reports none there. Shared by guards and civilians.
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
    private Vector3 lastPosition;

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
        lastPosition = transform.position;
    }

    private void Update()
    {
        // Actual velocity, not desiredVelocity: a guard blocked by a doorway should stop its legs too.
        // On a NavMesh link the guard moves the transform itself (GuardBehaviour.CrossLink) and the agent
        // reports no velocity, so the legs follow how far the body moved since last frame instead
        Vector3 velocity = agent.isOnOffMeshLink && Time.deltaTime > 0f
            ? (transform.position - lastPosition) / Time.deltaTime
            : agent.velocity;
        velocity.y = 0f;

        animator.SetFloat(NpcAnimatorParams.SpeedId, velocity.magnitude, speedDampTime, Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f) return;

        float yaw = transform.eulerAngles.y;
        float turnRate = Mathf.DeltaAngle(lastYaw, yaw) / Time.deltaTime;
        lastYaw = yaw;

        // After every Update, so the next frame's link speed spans one whole frame of movement
        lastPosition = transform.position;

        animator.SetFloat(NpcAnimatorParams.TurnRateId, turnRate, turnDampTime, Time.deltaTime);
    }
}
