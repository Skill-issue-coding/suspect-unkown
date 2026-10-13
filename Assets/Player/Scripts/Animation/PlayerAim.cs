using UnityEngine;

// Owns two separate things: where the player looks, and where the player's body faces.
// The cursor moves instantly; a standing body does not. Small differences are absorbed by the
// spine (PlayerAimTwist), and anything past the twist limit is paid off by a turn-in-place clip
// that steps the feet round. Driving the transform straight from the cursor, as this did before,
// is what made the character pivot on frozen soles.
//
// While moving the body keeps following the cursor directly: the strafe blend tree already has
// the feet in motion, so there is nothing to step out of.
[RequireComponent(typeof(PlayerInputs))]
[RequireComponent(typeof(PlayerMotor))]
public class PlayerAim : MonoBehaviour
{
    // Clip length is read back from any state carrying this tag. PlayerLocomotionBuilder sets it.
    private const string TurnTag = "Turn";

    // Matches PlayerAnimator's threshold for standing still, so the turn system and the blend
    // tree agree on when the character is idle.
    private const float MoveThreshold = 0.1f;

    [Header("Aim")]
    [Tooltip("Turn speed in degrees per second while moving. 0 turns instantly.")]
    [SerializeField] private float _movingTurnSpeed = 0.0f;

    [Header("Turn In Place")]
    [Tooltip("How far the cursor may lead the body before the feet step round. Everything inside " +
             "this band is covered by the spine twist.")]
    [SerializeField] private float _twistLimit = 45.0f;
    [Tooltip("The turn ends once the body is this close to the cursor. Keep it well under Twist " +
             "Limit, or a finished turn retriggers itself at the boundary.")]
    [SerializeField] private float _turnExitAngle = 5.0f;
    [Tooltip("Degrees each turn clip covers. Both standing turn clips are 90.")]
    [SerializeField] private float _turnClipAngle = 90.0f;
    [Tooltip("Used while the turn state is still blending in, and if the clip length cannot be read.")]
    [SerializeField] private float _fallbackTurnSpeed = 150.0f;

    // World point under the cursor, on the horizontal plane at the player's feet.
    public Vector3 AimPoint { get; private set; }

    // That point as a heading. The turn logic compares angles, not positions.
    public float AimYaw { get; private set; }

    // Where the body faces. The only thing that writes transform.rotation.
    public float BodyYaw { get; private set; }

    // -1 turning left, 1 turning right, 0 standing still or moving. PlayerAnimator plays the
    // matching clip from this.
    public int TurnDirection { get; private set; }

    // Degrees the cursor leads the body by. PlayerAimTwist spends it on the spine.
    public float TurnError => Mathf.DeltaAngle(BodyYaw, AimYaw);

    private Camera _camera;
    private PlayerInputs _input;
    private PlayerMotor _motor;
    private Animator _animator;

    private void Awake()
    {
        _camera = Camera.main;
        _input = GetComponent<PlayerInputs>();
        _motor = GetComponent<PlayerMotor>();

        // Only ever read from, to pace the body against the turn clip that is actually playing.
        _animator = GetComponentInChildren<Animator>();

        BodyYaw = transform.eulerAngles.y;
        AimYaw = BodyYaw;
    }

    private void Update()
    {
        if (_camera == null) return;

        // While suspended (lockpicking) the cursor is locked to the screen centre; aiming at it would
        // turn the body away from the door
        if (_motor.IsMovementSuspended) return;

        if (!UpdateAim()) return;

        float speed = Vector3.ProjectOnPlane(_motor.Velocity, Vector3.up).magnitude;
        if (speed > MoveThreshold) UpdateMovingTurn();
        else UpdateTurnInPlace();

        transform.rotation = Quaternion.Euler(0.0f, BodyYaw, 0.0f);
    }

    private bool UpdateAim()
    {
        // A math plane instead of a physics raycast, so walls and props never steal the aim.
        Ray ray = _camera.ScreenPointToRay(_input.aim);
        Plane ground = new Plane(Vector3.up, transform.position);
        if (!ground.Raycast(ray, out float distance)) return false;

        AimPoint = ray.GetPoint(distance);

        Vector3 direction = AimPoint - transform.position;
        direction.y = 0.0f;
        if (direction.sqrMagnitude < 0.0001f) return false;

        AimYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        return true;
    }

    private void UpdateMovingTurn()
    {
        TurnDirection = 0;
        BodyYaw = _movingTurnSpeed > 0.0f
            ? Mathf.MoveTowardsAngle(BodyYaw, AimYaw, _movingTurnSpeed * Time.deltaTime)
            : AimYaw;
    }

    private void UpdateTurnInPlace()
    {
        float error = TurnError;

        if (TurnDirection == 0)
        {
            // Inside the band the spine does all the work and the feet stay where they are.
            if (Mathf.Abs(error) <= _twistLimit) return;
            TurnDirection = error > 0.0f ? 1 : -1;
        }

        // A turn commits to its side. Chasing the cursor back the other way mid-clip would
        // rotate the body against feet that are still stepping the original way.
        if (Mathf.Abs(error) <= _turnExitAngle || error * TurnDirection < 0.0f)
        {
            TurnDirection = 0;
            return;
        }

        float step = Mathf.Min(TurnRate() * Time.deltaTime, Mathf.Abs(error));
        BodyYaw = Mathf.Repeat(BodyYaw + TurnDirection * step + 180.0f, 360.0f) - 180.0f;
    }

    // The clip sets the pace: its own angle over its own length. Rotating faster than the feet
    // step is the skating this whole system exists to remove. Crouching has no turn clips, so it
    // falls through to the fallback speed and turns without stepping.
    private float TurnRate()
    {
        if (_animator == null) return _fallbackTurnSpeed;

        AnimatorStateInfo state = _animator.IsInTransition(0)
            ? _animator.GetNextAnimatorStateInfo(0)
            : _animator.GetCurrentAnimatorStateInfo(0);

        if (!state.IsTag(TurnTag) || state.length <= 0.0f) return _fallbackTurnSpeed;

        return _turnClipAngle / state.length;
    }
}
