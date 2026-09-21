using UnityEngine;

// Plants the feet on uneven ground.
//
// This lives on the character rig rather than the player root because Unity sends OnAnimatorIK
// only to components on the Animator's own GameObject. It reads grounded state from the
// PlayerMotor above it in the hierarchy.
//
// Needs "IK Pass" enabled on the animator layer; PlayerLocomotionBuilder sets that.
[RequireComponent(typeof(Animator))]
public class PlayerFootIk : MonoBehaviour
{
    // Optional per-clip curves marking when each foot is planted. See _useFootPlantCurves.
    private static readonly int LeftFootIkId = Animator.StringToHash("LeftFootIK");
    private static readonly int RightFootIkId = Animator.StringToHash("RightFootIK");

    [SerializeField] private bool _enableFootIk = true;
    [Tooltip("Layers the feet can stand on. Exclude the player's own layer.")]
    [SerializeField] private LayerMask _groundLayers = ~0;
    [Tooltip("How far above the animated foot to start the probe.")]
    [SerializeField] private float _probeHeight = 0.5f;
    [Tooltip("How far below the animated foot to keep looking for ground.")]
    [SerializeField] private float _probeDistance = 0.6f;
    [Tooltip("Distance from the sole to the foot bone's pivot. Raise until the feet stop sinking.")]
    [SerializeField] private float _ankleHeight = 0.12f;
    [Tooltip("Seconds for the hips to settle onto a new step height.")]
    [SerializeField] private float _hipSmoothing = 0.12f;
    [Tooltip("Read LeftFootIK/RightFootIK curves from the clips so swinging feet are not pinned " +
             "to the ground. Add those curves per clip in the FBX import settings first.")]
    [SerializeField] private bool _useFootPlantCurves = false;

    // Live solver state, read by PlayerFootIkEditor to draw the scene view overlay.
    public float HipOffset => _hipOffset;
    public float LeftFootWeight { get; private set; }
    public float RightFootWeight { get; private set; }
    public LayerMask GroundLayers => _groundLayers;
    public float ProbeHeight => _probeHeight;
    public float ProbeDistance => _probeDistance;
    public float AnkleHeight => _ankleHeight;

    private Animator _animator;
    private PlayerMotor _motor;

    private float _hipOffset;
    private float _hipOffsetVelocity;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _motor = GetComponentInParent<PlayerMotor>();

        if (_motor == null)
        {
            Debug.LogError($"{name}: no PlayerMotor on this object or any parent.", this);
            enabled = false;
            return;
        }

        // The humanoid IK goals do not exist on a generic rig, so the solver would do nothing.
        if (!_animator.isHuman)
        {
            Debug.LogError($"{name}: foot IK needs a Humanoid avatar; this rig is generic.", _animator);
            enabled = false;
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!_enableFootIk || layerIndex != 0) return;

        // In the air the feet should follow the clip, not the ground under them.
        if (!_motor.IsGrounded)
        {
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0.0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0.0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0.0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0.0f);
            _hipOffset = Mathf.SmoothDamp(_hipOffset, 0.0f, ref _hipOffsetVelocity, _hipSmoothing);
            return;
        }

        bool leftGrounded = TryProbeFoot(AvatarIKGoal.LeftFoot, out Vector3 leftTarget, out Quaternion leftRotation);
        bool rightGrounded = TryProbeFoot(AvatarIKGoal.RightFoot, out Vector3 rightTarget, out Quaternion rightRotation);

        // The hips drop to whichever foot needs to reach furthest down, otherwise that leg
        // hyperextends trying to touch a step below it.
        float drop = 0.0f;
        if (leftGrounded) drop = Mathf.Min(drop, leftTarget.y - _animator.GetIKPosition(AvatarIKGoal.LeftFoot).y);
        if (rightGrounded) drop = Mathf.Min(drop, rightTarget.y - _animator.GetIKPosition(AvatarIKGoal.RightFoot).y);

        _hipOffset = Mathf.SmoothDamp(_hipOffset, drop, ref _hipOffsetVelocity, _hipSmoothing);
        _animator.bodyPosition += Vector3.up * _hipOffset;

        ApplyFoot(AvatarIKGoal.LeftFoot, LeftFootIkId, leftGrounded, leftTarget, leftRotation);
        ApplyFoot(AvatarIKGoal.RightFoot, RightFootIkId, rightGrounded, rightTarget, rightRotation);
    }

    private bool TryProbeFoot(AvatarIKGoal goal, out Vector3 position, out Quaternion rotation)
    {
        Vector3 animated = _animator.GetIKPosition(goal);
        Vector3 origin = animated + Vector3.up * _probeHeight;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _probeHeight + _probeDistance,
                _groundLayers, QueryTriggerInteraction.Ignore))
        {
            position = animated;
            rotation = _animator.GetIKRotation(goal);
            return false;
        }

        position = hit.point + Vector3.up * _ankleHeight;
        // Tilt the foot to the slope while keeping the animated toe direction.
        rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * _animator.GetIKRotation(goal);
        return true;
    }

    private void ApplyFoot(AvatarIKGoal goal, int plantCurveId, bool grounded, Vector3 position, Quaternion rotation)
    {
        // A planted foot should reach its ground; a swinging foot must stay on the clip, or the
        // stride gets pinned down and the character skates. Only the clip knows which is which,
        // hence the authored curve. Without it, fall back to "push up, never pull down", which
        // holds the feet out of geometry on rising ground but cannot fix a downward slope.
        float weight;
        if (_useFootPlantCurves)
        {
            weight = Mathf.Clamp01(_animator.GetFloat(plantCurveId)) * (grounded ? 1.0f : 0.0f);
        }
        else
        {
            float animatedY = _animator.GetIKPosition(goal).y + _hipOffset;
            weight = grounded && position.y > animatedY ? 1.0f : 0.0f;
        }

        if (goal == AvatarIKGoal.LeftFoot) LeftFootWeight = weight;
        else RightFootWeight = weight;

        _animator.SetIKPositionWeight(goal, weight);
        _animator.SetIKRotationWeight(goal, weight);

        if (weight <= 0.0f) return;

        _animator.SetIKPosition(goal, position);
        _animator.SetIKRotation(goal, rotation);
    }
}
