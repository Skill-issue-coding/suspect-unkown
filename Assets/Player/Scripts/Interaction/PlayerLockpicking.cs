using System;
using UnityEngine;

// Runs one lockpicking session at a door: feeds input to the LockpickModel, shows the view, holds the
// player still with the cursor locked, and opens the door when the lock turns. DoorInteraction
// starts it; the model decides, the view draws
[RequireComponent(typeof(PlayerInputs))]
[RequireComponent(typeof(PlayerMotor))]
public class PlayerLockpicking : MonoBehaviour
{
    [SerializeField, Tooltip("Difficulty per lock level and the shared feel values")]
    private LockpickTuning tuning;

    [SerializeField, Tooltip("The minigame's scene instance, not the prefab asset; shown while picking")]
    private LockpickView view;

    private PlayerMotor playerMotor;
    private PlayerInteraction interaction;

    public bool IsPicking { get; private set; }

    // Raised when a session stops, whether the lock opened or the player gave up
    public event Action Ended;

    [SerializeField, Tooltip("Seconds the opened lock stays on screen before the view closes")]
    private float successHold = 0.45f;

    private LockpickModel model;
    private Door targetDoor;
    private PlayerInputs _input;
    private float finishTimer;
    private bool cursorWasLocked;

    private void Awake()
    {
        _input = GetComponent<PlayerInputs>();
        playerMotor = GetComponent<PlayerMotor>();
        interaction = GetComponent<PlayerInteraction>();

        if (tuning == null || view == null)
        {
            Debug.LogWarning($"{name} needs a LockpickTuning and a LockpickView", this);
            enabled = false;
        }
    }

    public void Begin(Door door)
    {
        if (!enabled || IsPicking || door == null) return;

        targetDoor = door;
        IsPicking = true;

        LockLevelTuning levelTuning = tuning.levels[door.LockLevel - 1];
        model = new LockpickModel(levelTuning, tuning.returnSpeed, tuning.slipLockout, door.SweetSpot);

        view.gameObject.SetActive(true);
        view.Begin(model, door.LockLevel);

        if (interaction != null) interaction.IsBusy = true;

        playerMotor.IsMovementSuspended = true;

        // Locked and hidden: the pick reads mouse delta, which keeps coming while the cursor stays put
        cursorWasLocked = _input.cursorLocked;
        _input.SetCursorLocked(true);

        finishTimer = 0f;
        _input.tension = false;
        _input.cancel = false;
    }

    public void Cancel()
    {
        if (!IsPicking) return;

        IsPicking = false;
        model = null;
        targetDoor = null;
        view.End();
        view.gameObject.SetActive(false);

        playerMotor.IsMovementSuspended = false;
        _input.SetCursorLocked(cursorWasLocked);

        if (interaction != null) interaction.IsBusy = false;

        Ended?.Invoke();
    }

    private void Update()
    {
        if (!IsPicking) return;

        if (_input.cancel)
        {
            _input.cancel = false; // Consume the input so it doesn't fire twice
            Cancel();
            return;
        }

        // The lock is open and the door already swinging: hold the view a moment so the player sees it
        if (finishTimer > 0f)
        {
            finishTimer -= Time.deltaTime;
            if (finishTimer <= 0f) Cancel();
            return;
        }

        // Inverted so the pick follows the hand
        float moveInput = -_input.look.x * tuning.pickSpeed;

        model.Tick(Time.deltaTime, moveInput, _input.tension);

        if (model.IsOpen)
        {
            targetDoor.Open();
            if (successHold > 0f) finishTimer = successHold;
            else Cancel();
        }
    }
}
