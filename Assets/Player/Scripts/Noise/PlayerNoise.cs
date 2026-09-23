using UnityEngine;

[RequireComponent(typeof(PlayerStance))]
[RequireComponent(typeof(PlayerMotor))]
public class PlayerNoise : MonoBehaviour
{
    [SerializeField] private float CrouchNoiseRadius = 1.0f;
    [SerializeField] private float NormalNoiseRadius = 1.5f;
    [SerializeField] private float SprintingNoiseRadius = 3.0f;

    [SerializeField] private SphereCollider PlayerNoiseCollider;

    private PlayerStance _playerStance;

    private PlayerMotor _motor;

    private void Awake()
    {
        _playerStance = GetComponent<PlayerStance>();
        _motor = GetComponent<PlayerMotor>();
    }

    private void Update()
    {
        PlayerNoiseCollider.radius =
            _playerStance.Current == Stance.Crouching
                ? CrouchNoiseRadius
                : _motor.IsSprinting
                ? SprintingNoiseRadius
                : NormalNoiseRadius;
    }
}
