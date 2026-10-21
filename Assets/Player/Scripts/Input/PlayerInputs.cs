using System;
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
    public Vector2 look;   // pointer delta or right stick, this frame. Still moves while the cursor is locked
    public bool jump;
    public bool sprint;
    public bool crouch;

    [Header("Lockpicking Input Values")]
    public bool tension;
    public bool interact;
    public bool cancel;

    [Header("Mouse Cursor Settings")]
    public bool cursorLocked = false;

    public event Action SecurityDebugPressed;

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

    public void OnLook(InputValue value) { look = value.Get<Vector2>(); }

    public void OnJump(InputValue value) { JumpInput(value.isPressed); }

    public void OnSprint(InputValue value) { SprintInput(value.isPressed); }

    public void OnCrouch(InputValue value) { CrouchInput(value.isPressed); }

    public void MoveInput(Vector2 newMoveDirection) { move = newMoveDirection; }

    public void AimInput(Vector2 newAimPosition) { aim = newAimPosition; }

    public void JumpInput(bool newJumpState) { jump = newJumpState; }

    public void SprintInput(bool newSprintState) { sprint = newSprintState; }

    public void CrouchInput(bool newCrouchState) { crouch = newCrouchState; }

    // An event rather than a bool: a press is a moment, and readers would otherwise have to poll and
    // consume it every frame
    public event Action InteractPressed;

    public void OnInteract(InputValue value)
    {
        interact = value.isPressed;
        if (value.isPressed) InteractPressed?.Invoke();
    }

    public event Action DropPressed;

    public void OnDrop(InputValue value)
    {
        if (value.isPressed) DropPressed?.Invoke();
    }

    public void OnTension(InputValue value) { tension = value.isPressed; }

    public void OnCancel(InputValue value) { cancel = value.isPressed; }

    // Developer key for the adaptive-security overlay
    public void OnSecurityDebug(InputValue value)
    {
        if (value.isPressed) SecurityDebugPressed?.Invoke();
    }

    // The one place the cursor is locked or freed, so regaining window focus restores the right state
    public void SetCursorLocked(bool locked)
    {
        cursorLocked = locked;
        SetCursorState(locked);
    }

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