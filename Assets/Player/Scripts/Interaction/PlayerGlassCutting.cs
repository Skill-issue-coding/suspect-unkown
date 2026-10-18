using UnityEngine;

// Runs one glass-cutting session at a pane: feeds the pointer and tension to the GlassCutterModel, shows
// the view, holds the player still with the cursor free, and tells the glass when the circle is cut.
// GlassCutterInteraction starts it; the model decides, the view draws
[RequireComponent(typeof(PlayerInputs))]
[RequireComponent(typeof(PlayerMotor))]
public class PlayerGlassCutting : MonoBehaviour
{
    [SerializeField, Tooltip("Cut speed and aim tolerance")]
    private GlassCutterTuning tuning;

    [SerializeField, Tooltip("The minigame's scene instance, not the prefab asset; shown while cutting")]
    private GlassCutterView view;

    [SerializeField, Tooltip("Seconds the finished cut stays on screen before the view closes")]
    private float successHold = 0.45f;

    private PlayerMotor playerMotor;
    private PlayerInteraction interaction;
    private PlayerInputs _input;

    private GlassCutterModel model;
    private GlassCutterInteraction targetGlass;
    private float finishTimer;
    private bool isCutting;
    private bool cursorWasLocked;

    private void Awake()
    {
        _input = GetComponent<PlayerInputs>();
        playerMotor = GetComponent<PlayerMotor>();
        interaction = GetComponent<PlayerInteraction>();

        if (tuning == null || view == null)
        {
            Debug.LogWarning($"{name} needs a GlassCutterTuning and a GlassCutterView", this);
            enabled = false;
        }
    }

    public void Begin(GlassCutterInteraction glass)
    {
        if (!enabled || isCutting || glass == null) return;

        targetGlass = glass;
        isCutting = true;
        model = new GlassCutterModel(tuning.CutSpeed);

        view.gameObject.SetActive(true);
        view.Begin(model, tuning.CutClockwise);

        if (interaction != null) interaction.IsBusy = true;

        playerMotor.IsMovementSuspended = true;

        // Free cursor: the cone follows the pointer
        cursorWasLocked = _input.cursorLocked;
        _input.SetCursorLocked(false);

        finishTimer = 0f;
        _input.tension = false;
        _input.cancel = false;
    }

    private void End()
    {
        if (!isCutting) return;

        isCutting = false;
        model = null;
        targetGlass = null;
        view.End();
        view.gameObject.SetActive(false);

        playerMotor.IsMovementSuspended = false;
        _input.SetCursorLocked(cursorWasLocked);

        if (interaction != null) interaction.IsBusy = false;
    }

    private void Update()
    {
        if (!isCutting) return;

        if (_input.cancel)
        {
            _input.cancel = false; // Consume the input so it doesn't fire twice
            End();
            return;
        }

        // The circle is cut and the pane already gone: hold the view a moment so the player sees it
        if (finishTimer > 0f)
        {
            finishTimer -= Time.deltaTime;
            if (finishTimer <= 0f) End();
            return;
        }

        bool aimed = view.TryGetAim(_input.aim, tuning.MinAimDistance, out float angleToTangent);
        model.Tick(Time.deltaTime, aimed && angleToTangent <= tuning.AlignmentTolerance, _input.tension);

        if (model.IsComplete)
        {
            targetGlass.OnCut();
            if (successHold > 0f) finishTimer = successHold;
            else End();
        }
    }
}
