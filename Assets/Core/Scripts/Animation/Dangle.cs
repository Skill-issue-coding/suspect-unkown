using UnityEngine;

// Procedural pendulum for hanging accessories (keys, tags, the gym bag). Simulates a virtual
// weight on a fixed-length string below this transform and rotates the transform to point at it.
// Runs in LateUpdate, after the Animator has posed the parent bone, so it follows any animation
// with no Rigidbody, joint, or physics timing. Local hangAxis must point from the pivot toward
// the hanging end in the rest pose.
public class Dangle : MonoBehaviour
{
    [SerializeField, Tooltip("Local axis pointing from the pivot toward the hanging end, in the rest pose")]
    private Vector3 hangAxis = Vector3.down;

    [SerializeField, Range(0.005f, 0.5f), Tooltip("Metres from the pivot to the virtual weight. Longer swings slower")]
    private float length = 0.05f;

    [SerializeField, Range(0f, 3f), Tooltip("Multiplier on Physics.gravity")]
    private float gravityScale = 1f;

    [SerializeField, Range(0f, 20f), Tooltip("How quickly swinging dies out, per second. Only the swing is damped, not the carrier's own movement")]
    private float damping = 4f;

    [SerializeField, Range(0f, 20f), Tooltip("Pull back toward the authored rest direction, per second. 0 is pure gravity")]
    private float restPull = 2f;

    [SerializeField, Range(0f, 180f), Tooltip("Largest swing away from rest, in degrees, so keys do not pass through the leg. Eased in from half this angle")]
    private float maxAngle = 70f;

    [SerializeField, Range(0f, 60f), Tooltip("How quickly the shown rotation follows the simulation, per second. Filters frame-to-frame shake. 0 shows the raw simulation")]
    private float smoothing = 20f;

    [SerializeField, Tooltip("A pivot jump larger than this in one frame resets the swing (spawns, teleports)")]
    private float teleportDistance = 1f;

    // Longest step simulated in one frame. A hitch longer than this would fling the weight
    private const float MaxStep = 1f / 30f;

    private Quaternion restLocalRotation;
    private Vector3 tip;
    private Vector3 previousTip;
    private Vector3 previousPivot;
    private Vector3 previousPivotStep;
    private float previousStep;

    // The swing actually shown, relative to the animated pose. Eases toward the simulated swing
    private Quaternion shownSwing = Quaternion.identity;

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

        // Verlet: velocity is implicit in the last two positions. Rescale last frame's movement to
        // this frame's length, or uneven frame times read as sudden speed changes and the weight shakes
        float stepRatio = dt / previousStep;
        Vector3 velocity = (tip - previousTip) * stepRatio;

        // Damp only the swing relative to the pivot. Damping the whole velocity drags the weight
        // behind anything that moves, pinning it against maxAngle while the carrier runs
        Vector3 carried = previousPivotStep * stepRatio;
        velocity = carried + (velocity - carried) * Mathf.Exp(-damping * dt);

        previousTip = tip;
        previousPivotStep = pivot - previousPivot;
        previousPivot = pivot;
        previousStep = dt;

        tip += velocity + Physics.gravity * (gravityScale * dt * dt);

        Vector3 restTip = pivot + restDirection * length;
        tip = Vector3.Lerp(tip, restTip, 1f - Mathf.Exp(-restPull * dt));

        // Constrain: fixed string length, and a soft angle limit. A hard clamp stops the weight dead
        // at the limit, and the next frame's bounce off it reads as jitter
        Vector3 direction = (tip - pivot).normalized;
        float angle = SoftLimit(Vector3.Angle(restDirection, direction));
        direction = Vector3.RotateTowards(restDirection, direction, angle * Mathf.Deg2Rad, 0f);
        tip = pivot + direction * length;

        // Ease the shown swing toward the simulated one, in local space so turning the carrier does not drag it
        Quaternion swing = Quaternion.Inverse(transform.rotation) * Quaternion.FromToRotation(restDirection, direction) * transform.rotation;
        float follow = smoothing > 0f ? 1f - Mathf.Exp(-smoothing * dt) : 1f;
        shownSwing = Quaternion.Slerp(shownSwing, swing, follow);
        transform.localRotation = restLocalRotation * shownSwing;
    }

    // Unchanged up to half of maxAngle, then eases toward maxAngle without ever reaching a hard stop
    private float SoftLimit(float angle)
    {
        float knee = maxAngle * 0.5f;
        float range = maxAngle - knee;
        if (angle <= knee || range <= 0f) return Mathf.Min(angle, maxAngle);
        return knee + range * (1f - Mathf.Exp(-(angle - knee) / range));
    }

    private void ResetSwing()
    {
        previousPivot = transform.position;
        previousPivotStep = Vector3.zero;
        previousStep = MaxStep;
        tip = transform.position + transform.rotation * hangAxis.normalized * length;
        previousTip = tip;
        shownSwing = Quaternion.identity;
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
