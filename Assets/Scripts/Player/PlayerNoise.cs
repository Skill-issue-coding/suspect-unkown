using UnityEngine;

[RequireComponent(typeof(PlayerStance))]
public class PlayerNoise : MonoBehaviour
{
    [SerializeField] private float CrouchNoiseRadius = 1.0f;
    [SerializeField] private float NormalNoiseRadius = 1.5f;
    [SerializeField] private float SprintingNoiseRadius = 3.0f;

    [SerializeField] private SphereCollider PlayerNoiseCollider;

    private PlayerStance _playerStance;

    private void Awake()
    {
        _playerStance = GetComponent<PlayerStance>();
    }

    private void Start()
    {
        _playerStance.StanceChanged += UpdateNoiseRadius;
    }

    private void UpdateNoiseRadius(Stance stance)
    {
        PlayerNoiseCollider.radius = stance == Stance.Crouching ? CrouchNoiseRadius : stance == Stance.Standing ? NormalNoiseRadius : SprintingNoiseRadius;
    }
}
