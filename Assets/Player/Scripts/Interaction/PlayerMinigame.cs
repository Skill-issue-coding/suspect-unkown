using UnityEngine;

// Runs whichever minigame the player is in: opens its screen, holds the player still, sets the cursor,
// marks PlayerInteraction busy, ticks it each frame, ends it on cancel, and holds the finished view a
// moment before closing. The session code every minigame shared, kept once; an IMinigame supplies only
// its own rules, input and effect on the world. One per player
[RequireComponent(typeof(PlayerInputs))]
[RequireComponent(typeof(PlayerMotor))]
public class PlayerMinigame : MonoBehaviour
{
    [SerializeField, Tooltip("Seconds the finished minigame stays on screen before the view closes")]
    private float successHold = 0.45f;

    private PlayerInputs input;
    private PlayerMotor motor;
    private PlayerInteraction interaction;

    private IMinigame current;
    private float finishTimer;
    private bool cursorWasLocked;

    public bool IsPlaying => current != null;

    // Read by PlayerConduct; None outside a minigame
    public ConductFlags Conduct => current != null ? current.Conduct : ConductFlags.None;

    private void Awake()
    {
        input = GetComponent<PlayerInputs>();
        motor = GetComponent<PlayerMotor>();
        interaction = GetComponent<PlayerInteraction>();
    }

    // The minigame builds its model first, then calls this, then starts its own view. False when one
    // is already running or the minigame has no screen
    public bool Begin(IMinigame minigame)
    {
        if (!enabled || IsPlaying || minigame == null || minigame.Screen == null) return false;

        current = minigame;
        finishTimer = 0f;

        minigame.Screen.Open(minigame.Model);

        if (interaction != null) interaction.IsBusy = true;
        motor.IsMovementSuspended = true;

        cursorWasLocked = input.cursorLocked;
        input.SetCursorLocked(minigame.LocksCursor);

        input.tension = false;
        input.cancel = false;
        return true;
    }

    // Ends the session whether it completed or the player gave up
    public void End()
    {
        if (current == null) return;

        // Cleared first so nothing called below can start or end a session against the old one
        IMinigame ended = current;
        current = null;

        ended.Stop();
        ended.Screen.Close();

        motor.IsMovementSuspended = false;
        input.SetCursorLocked(cursorWasLocked);

        if (interaction != null) interaction.IsBusy = false;
    }

    private void Update()
    {
        if (current == null) return;

        if (input.cancel)
        {
            input.cancel = false; // Consume the input so it doesn't fire twice
            End();
            return;
        }

        // Complete and the world already changed: hold the view a moment so the player sees it
        if (finishTimer > 0f)
        {
            finishTimer -= Time.deltaTime;
            if (finishTimer <= 0f) End();
            return;
        }

        current.Tick(Time.deltaTime);

        if (current.Model.IsComplete)
        {
            current.Complete();
            if (successHold > 0f) finishTimer = successHold;
            else End();
        }
    }
}
