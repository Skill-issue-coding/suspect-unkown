using UnityEngine;

// Lockpicking as a minigame: builds a LockpickModel for the door's lock, feeds it the pick and tension
// input, and opens the door when the lock turns. PlayerMinigame runs the session around it;
// DoorInteraction starts it; the model decides, the view draws
[RequireComponent(typeof(PlayerInputs))]
[RequireComponent(typeof(PlayerMinigame))]
public class PlayerLockpicking : MonoBehaviour, IMinigame
{
    [SerializeField, Tooltip("Difficulty per lock level and the shared feel values")]
    private LockpickTuning tuning;

    private PlayerInputs input;
    private PlayerMinigame minigame;
    private LockpickView view;
    private MinigameScreen screen;

    private LockpickModel model;
    private Door targetDoor;

    public MinigameScreen Screen => screen;
    public IMinigameModel Model => model;

    // Locked and hidden: the pick reads mouse delta, which keeps coming while the cursor stays put
    public bool LocksCursor => true;
    public ConductFlags Conduct => ConductFlags.Lockpicking;

    private void Awake()
    {
        input = GetComponent<PlayerInputs>();
        minigame = GetComponent<PlayerMinigame>();

        if (tuning == null)
        {
            Debug.LogWarning($"{name} needs a LockpickTuning", this);
            enabled = false;
        }
    }

    public void Begin(Door door)
    {
        if (!enabled || door == null || minigame.IsPlaying || !FindView()) return;

        targetDoor = door;
        model = new LockpickModel(tuning.levels[door.LockLevel - 1], tuning.returnSpeed, tuning.slipLockout, door.SweetSpot);

        if (!minigame.Begin(this))
        {
            model = null;
            targetDoor = null;
            return;
        }

        view.Begin(model, door.LockLevel);
    }

    public void Tick(float deltaTime)
    {
        // Inverted so the pick follows the hand
        float moveInput = -input.look.x * tuning.pickSpeed;
        model.Tick(deltaTime, moveInput, input.tension);
    }

    public void Complete() => targetDoor.Open();

    public void Stop()
    {
        view.End();
        model = null;
        targetDoor = null;
    }

    // The view lives in the scene's MinigameRig; found on first use, so the order of the two objects'
    // Awake calls does not matter
    private bool FindView()
    {
        if (view != null) return true;

        if (MinigameRig.Current != null)
            view = MinigameRig.Current.GetComponentInChildren<LockpickView>(true);

        if (view == null)
        {
            Debug.LogWarning($"{name} found no LockpickView in the scene's MinigameRig", this);
            enabled = false;
            return false;
        }

        // LockpickView requires a MinigameScreen beside it
        screen = view.GetComponent<MinigameScreen>();
        return true;
    }
}
