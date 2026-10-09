using UnityEngine;
using UnityEngine.Animations.Rigging;

// Turns an NPC's head and neck toward a point through Animation Rigging aim constraints, so it can
// glance at something without turning its body. Executes only: the role behaviour decides what to
// look at with LookAt and ClearLook. Beyond maxAngle the look fades out and the body turn takes over.
// Runs in LateUpdate so it follows the animated pose. Any NPC with a rig can use it.
public class NpcHeadLook : MonoBehaviour
{
    [SerializeField] private MultiAimConstraint headAim;
    [SerializeField, Tooltip("Optional. Spreads the turn over the neck")]
    private MultiAimConstraint neckAim;
    [SerializeField, Tooltip("Empty under the NPC root that both constraints aim at")]
    private Transform lookTarget;

    [SerializeField, Tooltip("Metres above a ground-level point to aim at: roughly a person's head")]
    private float eyeLevel = 1.5f;
    [SerializeField, Range(0f, 0.5f), Tooltip("Seconds for the look point to catch up. Keep below the body's turn smoothing")]
    private float targetSmoothTime = 0.1f;
    [SerializeField, Tooltip("Constraint weight change per second when a look starts or stops")]
    private float blendSpeed = 4f;
    [SerializeField, Range(0f, 180f), Tooltip("Beyond this angle from the body's forward, the look fades and the body turns instead")]
    private float maxAngle = 110f;
    [SerializeField, Range(0f, 1f), Tooltip("Neck share of the turn")]
    private float neckWeight = 0.4f;

    private Vector3 desiredPoint;
    private bool looking;
    private Vector3 targetVelocity;
    private float weight;

    public void LookAt(Vector3 groundPoint)
    {
        desiredPoint = groundPoint + Vector3.up * eyeLevel;
        looking = true;
    }

    public void ClearLook() => looking = false;

    private void Awake()
    {
        if (headAim == null || lookTarget == null)
        {
            Debug.LogWarning($"{name} is missing its head aim or look target; NpcHeadLook disabled.", this);
            enabled = false;
            return;
        }

        lookTarget.position = transform.position + transform.forward * 2f + Vector3.up * eyeLevel;
        desiredPoint = lookTarget.position;
    }

    private void LateUpdate()
    {
        Vector3 toPoint = desiredPoint - transform.position;
        toPoint.y = 0f;
        bool inRange = looking && Vector3.Angle(transform.forward, toPoint) <= maxAngle;

        weight = Mathf.MoveTowards(weight, inRange ? 1f : 0f, blendSpeed * Time.deltaTime);
        headAim.weight = weight;
        if (neckAim != null) neckAim.weight = weight * neckWeight;

        if (inRange)
            lookTarget.position = Vector3.SmoothDamp(lookTarget.position, desiredPoint, ref targetVelocity, targetSmoothTime);
    }
}
