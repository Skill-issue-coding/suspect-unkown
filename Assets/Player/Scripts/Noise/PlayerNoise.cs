using UnityEngine;

// Publishes a footstep on NoiseBus every strideLength metres the player moves on the ground. Louder
// and more alarming when sprinting, quieter when crouching. Replaces the old trigger sphere, which let
// sound pass through walls: listeners now measure distance along the NavMesh (decision 10).
[RequireComponent(typeof(PlayerStance))]
[RequireComponent(typeof(PlayerMotor))]
public class PlayerNoise : MonoBehaviour
{
    [Header("Radius (metres the sound carries)")]
    [SerializeField, Tooltip("Crouched footsteps.")] private float _crouchRadius = 1f;
    [SerializeField, Tooltip("Walking footsteps.")] private float _walkRadius = 2.5f;
    [SerializeField, Tooltip("Sprinting footsteps.")] private float _sprintRadius = 9f;

    [Header("Alarm (0 harmless, 1 unmistakable)")]
    [SerializeField, Range(0f, 1f), Tooltip("Sprinting footsteps. Walking and crouching are always 0.")]
    private float _sprintAlarm = 0.3f;

    [Header("Steps")]
    [SerializeField, Tooltip("Metres moved between footsteps.")] private float _strideLength = 0.75f;

    private PlayerStance _stance;
    private PlayerMotor _motor;
    private float _distanceSinceStep;

    private void Awake()
    {
        _stance = GetComponent<PlayerStance>();
        _motor = GetComponent<PlayerMotor>();
    }

    private void Update()
    {
        if (!_motor.IsGrounded) return;

        Vector3 planar = Vector3.ProjectOnPlane(_motor.Velocity, Vector3.up);
        _distanceSinceStep += planar.magnitude * Time.deltaTime;
        if (_distanceSinceStep < _strideLength) return;

        _distanceSinceStep -= _strideLength;
        Step();
    }

    private void Step()
    {
        bool crouching = _stance.Current == Stance.Crouching;
        bool sprinting = !crouching && _motor.IsSprinting;

        float radius = crouching ? _crouchRadius : sprinting ? _sprintRadius : _walkRadius;
        float alarm = sprinting ? _sprintAlarm : 0f;

        NoiseBus.Publish(new NoiseEvent(transform.position, radius, alarm));
    }
}
