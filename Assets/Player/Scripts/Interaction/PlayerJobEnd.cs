using UnityEngine;
using UnityEngine.InputSystem;

// Takes the player's controls away the moment the job ends, caught or escaped: cancels a lockpick in
// progress, frees the cursor, stops movement, and stops all gameplay input so nothing new can start.
// The newspaper's buttons still work: UI clicks go through the EventSystem, not PlayerInput
[RequireComponent(typeof(PlayerInput), typeof(PlayerInputs), typeof(PlayerMotor))]
public class PlayerJobEnd : MonoBehaviour
{
    private PlayerInput playerInput;
    private PlayerInputs inputs;
    private PlayerMotor motor;
    private PlayerLockpicking lockpicking;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        inputs = GetComponent<PlayerInputs>();
        motor = GetComponent<PlayerMotor>();
        lockpicking = GetComponent<PlayerLockpicking>();
    }

    private void OnEnable()
    {
        JobOutcome.Failed += OnFailed;
        JobOutcome.Completed += End;
    }

    private void OnDisable()
    {
        JobOutcome.Failed -= OnFailed;
        JobOutcome.Completed -= End;
    }

    private void OnFailed(FailReason reason) => End();

    private void End()
    {
        if (lockpicking != null) lockpicking.Cancel();

        motor.IsMovementSuspended = true;
        inputs.MoveInput(Vector2.zero);
        inputs.SprintInput(false);
        inputs.interact = false;

        inputs.SetCursorLocked(false);

        playerInput.DeactivateInput();
    }
}
