using UnityEngine;
using UnityEngine.InputSystem;

// The player's input as plain values other scripts read, filled by PlayerInput's Send Messages
// (OnMove, OnAim, …). The only place input arrives: never read devices or the legacy Input class
// elsewhere. Input is ignored while the window is unfocused.
[RequireComponent(typeof(PlayerInput))]
public class PlayerInputs : MonoBehaviour
{
    [Header("Character Input Values")]
    public Vector2 move;
    public Vector2 aim;
    public bool jump;
    public bool sprint;
    public bool crouch;

    [Header("Lockpicking Input Values")]
    public bool tension;
    public bool cancel;

    [Header("Mouse Cursor Settings")]
    public bool cursorLocked = false;

    private bool m_IgnoreInput;

    static void ResetStatics() { }

    public void OnMove(InputValue value)
    {
        if (m_IgnoreInput)
        {
            MoveInput(Vector2.zero);
            return;
        }

        MoveInput(value.Get<Vector2>());
    }

    public void OnAim(InputValue value) { AimInput(value.Get<Vector2>()); }

    public void OnJump(InputValue value) { JumpInput(value.isPressed); }

    public void OnSprint(InputValue value) { SprintInput(value.isPressed); }

    public void OnCrouch(InputValue value) { CrouchInput(value.isPressed); }

    public void MoveInput(Vector2 newMoveDirection) { move = newMoveDirection; }

    public void AimInput(Vector2 newAimPosition) { aim = newAimPosition; }

    public void JumpInput(bool newJumpState) { jump = newJumpState; }

    public void SprintInput(bool newSprintState) { sprint = newSprintState; }

    public void CrouchInput(bool newCrouchState) { crouch = newCrouchState; }

    public void OnTension(InputValue value) { tension = value.isPressed; }
    
    public void OnCancel(InputValue value) { cancel = value.isPressed; }

    private void OnApplicationFocus(bool hasFocus)
    {
        SetCursorState(cursorLocked);
        m_IgnoreInput = !hasFocus;
    }

    private void SetCursorState(bool newState)
    {
        Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !newState;
    }
}