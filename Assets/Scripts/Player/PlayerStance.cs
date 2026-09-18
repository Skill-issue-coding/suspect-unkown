using System;
using UnityEngine;

public enum Stance
{
    Standing,
    Crouching,
    Sprinting
}

// Runs before PlayerMotor so the motor always reads this frame's stance.
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputs))]
public class PlayerStance : MonoBehaviour
{
    [Header("Crouch")]
    [Tooltip("Capsule height while crouching, as a fraction of standing height.")]
    [SerializeField, Range(0.1f, 1.0f)] private float _crouchHeightFactor = 0.5f;
    [Tooltip("Layers that block standing up from a crouch.")]
    [SerializeField] private LayerMask _ceilingLayers = ~0;

    public Stance Current { get; private set; } = Stance.Standing;

    public event Action<Stance> StanceChanged;

    private float _baseHeight;
    private Vector3 _baseCenter;

    private CharacterController _controller;
    private PlayerInputs _input;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _input = GetComponent<PlayerInputs>();

        _baseHeight = _controller.height;
        _baseCenter = _controller.center;
    }

    private void Update() { SetStance(ResolveStance()); }

    private Stance ResolveStance()
    {
        if (_input.crouch) return Stance.Crouching;

        // Stay crouched until there is room to stand.
        if (Current == Stance.Crouching && !CanStand()) return Stance.Crouching;

        if (_input.sprint && _input.move != Vector2.zero) return Stance.Sprinting;

        return Stance.Standing;
    }

    private void SetStance(Stance stance)
    {
        if (stance == Current) return;

        Current = stance;
        ApplyCapsule();
        StanceChanged?.Invoke(Current);
    }

    private void ApplyCapsule()
    {
        _controller.height = Current == Stance.Crouching ? _baseHeight * _crouchHeightFactor : _baseHeight;
        // Shift center down with height so the capsule bottom stays on the ground.
        _controller.center = _baseCenter + Vector3.down * ((_baseHeight - _controller.height) / 2.0f);
    }

    private bool CanStand()
    {
        float radius = _controller.radius;
        Vector3 top = transform.TransformPoint(_controller.center + Vector3.up * (_controller.height / 2.0f - radius));
        float distance = _baseHeight - _controller.height;

        // Casts ignore colliders already overlapping the start sphere, so the player's own capsule is not hit.
        return !Physics.SphereCast(top, radius, Vector3.up, out _, distance, _ceilingLayers, QueryTriggerInteraction.Ignore);
    }
}
