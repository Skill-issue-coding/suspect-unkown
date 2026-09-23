using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStance))]
[RequireComponent(typeof(PlayerInputs))]
public class PlayerMotor : MonoBehaviour
{
    // Small constant downward pull while grounded so isGrounded stays reliable on slopes and steps.
    private const float GroundedStickVelocity = -2.0f;

    [Header("Player Movement")]
    [SerializeField] private float _walkSpeed = 4.0f;
    [SerializeField] private float _sprintSpeed = 6.0f;
    [SerializeField] private float _crouchSpeed = 2.0f;
    [Tooltip("Speed gained per second when speeding up (m/s²).")]
    [SerializeField] private float _acceleration = 40.0f;
    [Tooltip("Speed lost per second when slowing down or turning (m/s²).")]
    [SerializeField] private float _deceleration = 60.0f;

    [Header("Jump and Gravity")]
    [Tooltip("Peak jump height in meters.")]
    [SerializeField] private float _jumpHeight = 1.2f;
    [SerializeField] private float _gravity = -15.0f;
    [SerializeField] private float _terminalVelocity = -50.0f;
    [Tooltip("Time after walking off a ledge during which a jump still works.")]
    [SerializeField] private float _coyoteTime = 0.1f;

    public bool IsSprinting { get; private set; }
    public Vector3 Velocity => _horizontalVelocity + Vector3.up * _verticalVelocity;
    public bool IsGrounded => _controller.isGrounded;

    // True once per launched jump, cleared by whoever reads it. A plain "jumped this frame" flag
    // would depend on script order: the reader may run before this component does.
    public bool ConsumeJump()
    {
        bool jumped = _jumpLaunched;
        _jumpLaunched = false;
        return jumped;
    }

    public float WalkSpeed => _walkSpeed;
    public float SprintSpeed => _sprintSpeed;
    public float CrouchSpeed => _crouchSpeed;

    private Vector3 _horizontalVelocity;
    private float _verticalVelocity;
    private float _lastGroundedTime = float.NegativeInfinity;
    private bool _jumpLaunched;

    private CharacterController _controller;
    private PlayerStance _stance;
    private PlayerInputs _input;
    private Transform _camera;

    private void Awake()
    {
        _camera = Camera.main != null ? Camera.main.transform : null;
        _controller = GetComponent<CharacterController>();
        _stance = GetComponent<PlayerStance>();
        _input = GetComponent<PlayerInputs>();
    }

    private void Update()
    {
        UpdateHorizontal();
        UpdateVertical();

        if (_controller.enabled)
        {
            CollisionFlags flags = _controller.Move(Velocity * Time.deltaTime);

            // Stop rising when the head hits a ceiling.
            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0.0f) _verticalVelocity = 0.0f;
        }
    }

    private void UpdateHorizontal()
    {
        IsSprinting = _input.sprint && _input.move != Vector2.zero && _stance.Current == Stance.Standing;
        float targetSpeed = _stance.Current switch
        {
            Stance.Crouching => _crouchSpeed,
            _ => IsSprinting ? _sprintSpeed : _walkSpeed
        };

        // Camera-relative so W is always "up the screen", no matter where the player faces.
        Vector3 forward = _camera != null ? Vector3.ProjectOnPlane(_camera.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = _camera != null ? Vector3.ProjectOnPlane(_camera.right, Vector3.up).normalized : Vector3.right;
        Vector3 moveDirection = (right * _input.move.x + forward * _input.move.y).normalized;
        Vector3 targetVelocity = moveDirection * targetSpeed;

        // Speeding up uses acceleration; stopping, slowing or reversing uses deceleration.
        float rate = targetVelocity.sqrMagnitude > _horizontalVelocity.sqrMagnitude ? _acceleration : _deceleration;
        _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, rate * Time.deltaTime);
    }

    private void UpdateVertical()
    {
        if (_controller.isGrounded)
        {
            _lastGroundedTime = Time.time;
            if (_verticalVelocity < 0.0f) _verticalVelocity = GroundedStickVelocity;
        }

        bool canJump = Time.time - _lastGroundedTime <= _coyoteTime && _stance.Current != Stance.Crouching;

        if (_input.jump && canJump)
        {
            // v = sqrt(2 * g * h) gives the exact peak height.
            _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2.0f * _gravity);
            _lastGroundedTime = float.NegativeInfinity;
            _jumpLaunched = true;
            // Consume the press so holding the key does not bunny hop.
            _input.jump = false;
        }

        _verticalVelocity = Mathf.Max(_verticalVelocity + _gravity * Time.deltaTime, _terminalVelocity);
    }
}
