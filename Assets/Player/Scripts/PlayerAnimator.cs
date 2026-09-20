using UnityEngine;

// Translates motor state into animator parameters.
// The character faces the cursor while moving camera-relative, so MoveX/MoveY are in local
// space: that is what lets the strafe clips play instead of always walking forward.
//
// The Animator lives on the character rig, which is a child of this object, so this script
// must not require one here. Foot IK lives in PlayerFootIk for the same reason: Unity sends
// OnAnimatorIK only to components sharing the Animator's GameObject.
[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(PlayerStance))]
[RequireComponent(typeof(PlayerAim))]
public class PlayerAnimator : MonoBehaviour
{
    // Positions on the locomotion blend tree's Speed axis.
    private const float IdleBlend = 0.0f;
    private const float WalkBlend = 1.0f;
    private const float SprintBlend = 2.0f;

    // Below this planar speed the character counts as standing still.
    private const float MoveThreshold = 0.1f;

    private static readonly int MoveXId = Animator.StringToHash("MoveX");
    private static readonly int MoveYId = Animator.StringToHash("MoveY");
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int CrouchingId = Animator.StringToHash("Crouching");
    private static readonly int GroundedId = Animator.StringToHash("Grounded");
    private static readonly int TurnId = Animator.StringToHash("Turn");
    private static readonly int JumpId = Animator.StringToHash("Jump");

    // Override layer holding the crouch arm pose. PlayerLocomotionBuilder names it.
    private const string CrouchArmsLayer = "Crouch Arms";

    [Header("Damping")]
    [Tooltip("Seconds for the direction parameters to catch up. Smooths diagonal changes.")]
    [SerializeField] private float _directionDamping = 0.1f;
    [Tooltip("Seconds for the speed parameter to catch up.")]
    [SerializeField] private float _speedDamping = 0.1f;
    [Tooltip("Seconds for the crouch arm override to fade in and out. Too fast and the arms snap " +
             "pose as the character drops into the crouch.")]
    [SerializeField] private float _crouchArmsDamping = 0.2f;

    private Animator _animator;
    private PlayerMotor _motor;
    private PlayerStance _stance;
    private PlayerAim _aim;

    // Kept so the blend tree holds its last direction while idle instead of snapping to centre.
    private Vector2 _direction = Vector2.up;

    // -1 when the controller has no crouch arm layer, which is the case if the crouch clips are
    // missing and the builder skipped it.
    private int _crouchArmsLayer = -1;
    private float _crouchArmsWeight;
    private float _crouchArmsVelocity;

    private void Awake()
    {
        // Searches this object first, then the rig below it.
        _animator = GetComponentInChildren<Animator>();
        _motor = GetComponent<PlayerMotor>();
        _stance = GetComponent<PlayerStance>();
        _aim = GetComponent<PlayerAim>();

        if (_animator == null)
        {
            Debug.LogError($"{name}: no Animator found on this object or its children.", this);
            enabled = false;
            return;
        }

        if (_animator.runtimeAnimatorController == null)
        {
            Debug.LogError($"{name}: the Animator on '{_animator.name}' has no controller assigned.", _animator);
            enabled = false;
            return;
        }

        _crouchArmsLayer = _animator.GetLayerIndex(CrouchArmsLayer);

        // PlayerMotor owns movement via the CharacterController. Leaving root motion on lets the
        // clips displace the rig as well, so the mesh drifts out of the capsule.
        if (_animator.applyRootMotion)
        {
            Debug.LogWarning($"{name}: disabling Apply Root Motion on '{_animator.name}'; " +
                             "PlayerMotor drives movement.", _animator);
            _animator.applyRootMotion = false;
        }
    }

    private void Update()
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(_motor.Velocity, Vector3.up);
        float speed = planarVelocity.magnitude;

        // Local space, so "forward" means "the way the character is facing", not the way it moves.
        Vector3 localVelocity = transform.InverseTransformDirection(planarVelocity);

        // Unit length keeps the 2D blend on the outer edge of the strafe clips; the Speed
        // parameter alone decides idle vs walk vs sprint. Blending toward the centre instead
        // would wash the clips out into a half-posed shuffle.
        if (speed > MoveThreshold) _direction = new Vector2(localVelocity.x, localVelocity.z).normalized;

        _animator.SetFloat(MoveXId, _direction.x, _directionDamping, Time.deltaTime);
        _animator.SetFloat(MoveYId, _direction.y, _directionDamping, Time.deltaTime);
        _animator.SetFloat(SpeedId, ResolveSpeedBlend(speed), _speedDamping, Time.deltaTime);
        _animator.SetBool(CrouchingId, _stance.Current == Stance.Crouching);
        _animator.SetBool(GroundedId, _motor.IsGrounded);

        // Undamped: the turn states switch on a threshold, so a ramp would only delay the step.
        _animator.SetFloat(TurnId, _aim.TurnDirection);

        // Set after Speed, so the standing/running choice is made against this frame's value.
        if (_motor.ConsumeJump()) _animator.SetTrigger(JumpId);

        UpdateCrouchArms();
    }

    // The crouch strafe clips hold a torch, so a masked layer replays the crouch forward clip's
    // arms over them. It is faded rather than switched: at full weight from one frame to the next
    // the arms jump pose as the character drops.
    private void UpdateCrouchArms()
    {
        if (_crouchArmsLayer < 0) return;

        float target = _stance.Current == Stance.Crouching ? 1.0f : 0.0f;
        _crouchArmsWeight = Mathf.SmoothDamp(_crouchArmsWeight, target, ref _crouchArmsVelocity,
            _crouchArmsDamping);

        _animator.SetLayerWeight(_crouchArmsLayer, _crouchArmsWeight);
    }

    // Maps metres per second onto the blend tree's thresholds, so retuning the motor's
    // speeds never desynchronises the footfalls from the ground.
    private float ResolveSpeedBlend(float speed)
    {
        if (speed <= MoveThreshold) return IdleBlend;

        // The crouch state has its own 0..1 tree, so it normalises against the crouch speed.
        if (_stance.Current == Stance.Crouching)
            return Mathf.Clamp(speed / Mathf.Max(_motor.CrouchSpeed, 0.01f), IdleBlend, WalkBlend);

        // Below walking pace, ramp idle to walk; above it, ramp walk to sprint. Staying monotonic
        // matters because a jump in this value would skip the walk tier when sprinting from rest.
        if (speed <= _motor.WalkSpeed)
            return Mathf.Lerp(IdleBlend, WalkBlend, speed / Mathf.Max(_motor.WalkSpeed, 0.01f));

        float t = Mathf.InverseLerp(_motor.WalkSpeed, _motor.SprintSpeed, speed);
        return Mathf.Lerp(WalkBlend, SprintBlend, t);
    }
}
