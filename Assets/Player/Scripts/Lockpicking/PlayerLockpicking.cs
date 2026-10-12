using UnityEngine;
using UnityEngine.InputSystem; // for the temporary 'T' test button

[RequireComponent(typeof(PlayerInputs))]
public class PlayerLockpicking : MonoBehaviour
{
    [SerializeField] private LockpickTuning tuning;
    [SerializeField] private LockpickView view;
    [SerializeField] private PlayerMotor playerMotor;
    
    public bool IsPicking { get; private set; }
    
    private LockpickModel model;
    private Door targetDoor;
    private PlayerInputs _input;
    private float _lastAimX;

    private void Awake()
    {
        // Grab the centralized input component
        _input = GetComponent<PlayerInputs>();
    }

    public void Begin(Door door)
    {
        targetDoor = door;
        IsPicking = true;
        
        LockLevelTuning levelTuning = tuning.levels[door.lockLevel - 1];
        model = new LockpickModel(levelTuning, tuning.returnSpeed, tuning.slipLockout, door.SweetSpot);
        
        view.gameObject.SetActive(true);
        view.Begin(model);
        
        if (playerMotor != null) playerMotor.IsMovementSuspended = true;

        // Capture the initial mouse position so the pick doesn't jerk on frame 1
        _lastAimX = _input.aim.x;
        _input.tension = false; // clear input
    }

    public void Cancel()
    {
        IsPicking = false;
        view.End();
        view.gameObject.SetActive(false);
        
        if (playerMotor != null) playerMotor.IsMovementSuspended = false;
    }

    private void Update()
    {
        // Temporary test button
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame && !IsPicking)
        {
            Begin(FindAnyObjectByType<Door>());
        }

        if (!IsPicking || model == null) return;

        // 1. Calculate mouse delta manually from the absolute aim position
        float deltaX = _input.aim.x - _lastAimX;
        _lastAimX = _input.aim.x;

        // Apply the delta with the inverted direction you requested earlier
        float moveInput = -deltaX * tuning.pickSpeed;
        
        // Read tension from PlayerInputs
        bool tensionInput = _input.tension;

        // Read cancel from PlayerInputs
        if (_input.cancel)
        {
            _input.cancel = false; // Consume the input so it doesn't fire twice
            Cancel();
            return;
        }

        model.Tick(Time.deltaTime, moveInput, tensionInput);

        if (model.IsOpen)
        {
            targetDoor.Open();
            Cancel();
        }
    }
}