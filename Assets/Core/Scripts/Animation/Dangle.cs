using UnityEngine;

// Procedural pendulum for small hanging accessories (keys, tags, a bag strap). Simulates a
// virtual weight on a fixed-length string below this transform and rotates the transform to
// point at it. Runs in LateUpdate, after the Animator has posed the parent bone, so it follows
// any animation with no Rigidbody, joint, or physics timing. Local -Y (hangAxis) must point from
// the pivot toward the hanging end in the rest pose.
public class Dangle : MonoBehaviour
{
    [SerializeField, Tooltip("Local axis pointing from the pivot toward the hanging end, in the rest pose")]
    private Vector3 hangAxis = Vector3.down;

    [SerializeField, Range(0.005f, 0.5f), Tooltip("Metres from the pivot to the virtual weight. Longer swings slower")]
    private float length = 0.05f;

    [SerializeField, Range(0f, 3f), Tooltip("Multiplier on Physics.gravity")]
    private float gravityScale = 1f;

    [SerializeField, Range(0f, 20f), Tooltip("How quickly swinging dies out")]
    private float damping = 4f;

    [SerializeField, Range(0f, 20f), Tooltip("Pull back toward the authored rest direction. 0 is pure gravity")]
    private float restPull = 2f;

    [SerializeField, Range(0f, 180f), Tooltip("Largest swing away from rest, so keys do not pass through the leg")]
    private float maxAngle = 70f;

    [SerializeField, Tooltip("A pivot jump larger than this in one frame resets the swing (spawns, teleports)")]
    private float teleportDistance = 1f;

    // Longest step simulated in one frame. A hitch longer than this would fling the weight
    private const float MaxStep = 1f / 30f;

    private Quaternion restLocalRotation;
    private Vector3 tip;
    private Vector3 previousTip;
    private Vector3 previousPivot;

    private void Awake() => restLocalRotation = transform.localRotation;

    private void OnEnable() => ResetSwing();

    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, MaxStep);
        if (dt <= 0f) return;

        // Start from the animated pose every frame, so the swing is always relative to the bone
        transform.localRotation = restLocalRotation;

        Vector3 pivot = transform.position;
        Vector3 restDirection = transform.rotation * hangAxis.normalized;

        if ((pivot - previousPivot).sqrMagnitude > teleportDistance * teleportDistance)
        {
            ResetSwing();
            return;
        }
        previousPivot = pivot;

        // Verlet: velocity is implicit in the last two positions
        Vector3 velocity = (tip - previousTip) * Mathf.Max(0f, 1f - damping * dt);
        previousTip = tip;
        tip += velocity + Physics.gravity * (gravityScale * dt * dt);

        Vector3 restTip = pivot + restDirection * length;
        tip = Vector3.Lerp(tip, restTip, restPull * dt);

        // Constrain: fixed string length, and no further than maxAngle from rest
        Vector3 direction = (tip - pivot).normalized;
        direction = Vector3.RotateTowards(restDirection, direction, maxAngle * Mathf.Deg2Rad, 0f);
        tip = pivot + direction * length;

        transform.rotation = Quaternion.FromToRotation(restDirection, direction) * transform.rotation;
    }

    private void ResetSwing()
    {
        previousPivot = transform.position;
        tip = transform.position + transform.rotation * hangAxis.normalized * length;
        previousTip = tip;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 pivot = transform.position;
        Vector3 end = Application.isPlaying ? tip : pivot + transform.rotation * hangAxis.normalized * length;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pivot, end);
        Gizmos.DrawSphere(end, 0.005f);
    }
}
