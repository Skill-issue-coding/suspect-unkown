using UnityEngine;

[RequireComponent(typeof(PlayerInputs))]
public class PlayerAim : MonoBehaviour
{
    [Tooltip("Turn speed in degrees per second. 0 turns instantly.")]
    [SerializeField] private float _turnSpeed = 0.0f;

    // World point under the cursor, on the horizontal plane at the player's feet.
    public Vector3 AimPoint { get; private set; }

    private Camera _camera;
    private PlayerInputs _input;

    private void Awake()
    {
        _camera = Camera.main;
        _input = GetComponent<PlayerInputs>();
    }

    private void Update()
    {
        if (_camera == null) return;

        // A math plane instead of a physics raycast, so walls and props never steal the aim.
        Ray ray = _camera.ScreenPointToRay(_input.aim);
        Plane ground = new Plane(Vector3.up, transform.position);
        if (!ground.Raycast(ray, out float distance)) return;

        AimPoint = ray.GetPoint(distance);

        Vector3 direction = AimPoint - transform.position;
        direction.y = 0.0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = _turnSpeed > 0.0f
            ? Quaternion.RotateTowards(transform.rotation, targetRotation, _turnSpeed * Time.deltaTime)
            : targetRotation;
    }
}
