using UnityEngine;
using UnityEngine.InputSystem;

// Takes the player's controls away the moment the job ends, caught or escaped: ends a minigame in
// progress, frees the cursor, stops movement, and stops all gameplay input so nothing new can start.
// The same happens while the leave-empty-handed warning is open, and Cancel hands the controls back.
// The newspaper's and the warning's buttons still work: UI clicks go through the EventSystem, not PlayerInput
[RequireComponent(typeof(PlayerInput), typeof(PlayerInputs), typeof(PlayerMotor))]
public class PlayerJobEnd : MonoBehaviour
{
    private PlayerInput playerInput;
    private PlayerInputs inputs;
    private PlayerMotor motor;
    private PlayerMinigame minigame;
    private bool cursorWasLocked;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        inputs = GetComponent<PlayerInputs>();
        motor = GetComponent<PlayerMotor>();
        minigame = GetComponent<PlayerMinigame>();
    }

    private void OnEnable()
    {
        JobOutcome.Failed += OnFailed;
        JobOutcome.Completed += End;
        JobOutcome.Exited += End;
        ExtractionWarning.Shown += OnWarningShown;
        ExtractionWarning.Hidden += OnWarningHidden;
    }

    private void OnDisable()
    {
        JobOutcome.Failed -= OnFailed;
        JobOutcome.Completed -= End;
        JobOutcome.Exited -= End;
        ExtractionWarning.Shown -= OnWarningShown;
        ExtractionWarning.Hidden -= OnWarningHidden;
    }

    private void OnFailed(FailReason reason) => End();

    private void End()
    {
        if (minigame != null) minigame.End();

        motor.IsMovementSuspended = true;
        inputs.MoveInput(Vector2.zero);
        inputs.SprintInput(false);
        inputs.interact = false;

        inputs.SetCursorLocked(false);

        playerInput.DeactivateInput();
    }

    // Gameplay leaves the cursor free for aiming, so remember its state rather than assume one
    private void OnWarningShown()
    {
        cursorWasLocked = inputs.cursorLocked;
        End();
    }

    // Cancelled: back to the job. A Continue has already ended the job by now, so the controls stay off
    private void OnWarningHidden()
    {
        if (JobOutcome.HasEnded) return;

        motor.IsMovementSuspended = false;
        inputs.SetCursorLocked(cursorWasLocked);
        playerInput.ActivateInput();
    }
}
