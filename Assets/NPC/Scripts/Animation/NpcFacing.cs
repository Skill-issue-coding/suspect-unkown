using UnityEngine;
using UnityEngine.AI;

// Turns an NPC's body: toward a facing a behaviour asked for this frame, otherwise toward where the
// agent is walking. Behaviours request during their Update; this applies once, after all of them.
// Shared by guards and civilians (decision 2). A request lasts one frame: ask again to keep facing
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(NavMeshAgent))]
public class NpcFacing : MonoBehaviour
{
    // Below 0.25 m/s the NPC keeps its facing: steering is noisy while starting and braking
    private const float MinFacingSpeedSqr = 0.25f * 0.25f;

    [SerializeField, Tooltip("Maximum degrees per second the body turns")]
    private float turnSpeed = 240f;

    [SerializeField, Range(0f, 2f), Tooltip("Seconds to catch up with a new facing. Higher is lazier")]
    private float turnSmoothTime = 0.2f;

    private NavMeshAgent agent;
    private float desiredYaw;
    private bool hasDesiredYaw;
    private float yawVelocity;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
    }

    public void LookAt(Vector3 point) => LookDirection(point - transform.position);

    public void LookDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        FaceYaw(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
    }

    public void FaceYaw(float yaw)
    {
        desiredYaw = yaw;
        hasDesiredYaw = true;
    }

    private void Update()
    {
        if (!hasDesiredYaw && agent.desiredVelocity.sqrMagnitude > MinFacingSpeedSqr)
            LookDirection(agent.desiredVelocity);

        if (hasDesiredYaw)
        {
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, desiredYaw, ref yawVelocity, turnSmoothTime, turnSpeed);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        hasDesiredYaw = false;
    }
}
