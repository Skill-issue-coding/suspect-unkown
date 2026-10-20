using UnityEngine;

// Safe cracking as a minigame: builds a SafeModel for the safe's combination, feeds it the dial and
// tension input, and opens the safe once every number is set. Numbers set before backing off stay set on
// the safe. PlayerMinigame runs the session around it; SafeInteraction starts it; the model decides, the
// view draws. Seen from outside it is lockpicking (thriftshop-level.md 2.3)
[RequireComponent(typeof(PlayerInputs))]
[RequireComponent(typeof(PlayerMinigame))]
public class PlayerSafeCracking : MonoBehaviour, IMinigame
{
    [SerializeField, Tooltip("Difficulty per safe level and the shared feel values")]
    private SafeTuning tuning;

    private PlayerInputs input;
    private PlayerMinigame minigame;
    private SafeView view;
    private MinigameScreen screen;

    private SafeModel model;
    private Safe targetSafe;

    public MinigameScreen Screen => screen;
    public IMinigameModel Model => model;

    // Locked and hidden: the dial reads mouse delta, like the pick
    public bool LocksCursor => true;
    public ConductFlags Conduct => ConductFlags.Lockpicking;

    private void Awake()
    {
        input = GetComponent<PlayerInputs>();
        minigame = GetComponent<PlayerMinigame>();

        if (tuning == null)
        {
            Debug.LogWarning($"{name} needs a SafeTuning", this);
            enabled = false;
        }
    }

    public void Begin(Safe safe)
    {
        if (!enabled || safe == null || safe.IsOpen || minigame.IsPlaying || !FindView()) return;

        SafeLevelTuning level = tuning.Level(safe.LockLevel);
        int[] combination = safe.CombinationFor(level.numbers, view.DialNumbers);

        targetSafe = safe;
        model = new SafeModel(level, combination, view.DialNumbers, safe.NumbersSet, tuning.ReturnSpeed, tuning.SlipLockout);

        if (!minigame.Begin(this))
        {
            model = null;
            targetSafe = null;
            return;
        }

        view.Begin(model);
    }

    public void Tick(float deltaTime)
    {
        // Mouse right turns the dial right (clockwise)
        model.Tick(deltaTime, input.look.x * tuning.DialSpeed, input.tension);
    }

    public void Complete() => targetSafe.Open();

    public void Stop()
    {
        targetSafe.RememberNumbers(model.NumbersSet);
        view.End();
        model = null;
        targetSafe = null;
    }

    // The view lives in the scene's MinigameRig; found on first use, so the order of the two objects'
    // Awake calls does not matter
    private bool FindView()
    {
        if (view != null) return true;

        if (MinigameRig.Current != null)
            view = MinigameRig.Current.GetComponentInChildren<SafeView>(true);

        if (view == null)
        {
            Debug.LogWarning($"{name} found no SafeView in the scene's MinigameRig", this);
            enabled = false;
            return false;
        }

        // SafeView requires a MinigameScreen beside it
        screen = view.GetComponent<MinigameScreen>();
        return true;
    }
}
