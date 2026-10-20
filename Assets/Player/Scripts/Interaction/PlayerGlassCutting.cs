using UnityEngine;

// Glass cutting as a minigame: feeds the pointer and tension to a GlassCutterModel, and tells the glass
// when the circle is cut. PlayerMinigame runs the session around it; GlassCutterInteraction starts it;
// the model decides, the view draws
[RequireComponent(typeof(PlayerInputs))]
[RequireComponent(typeof(PlayerMinigame))]
public class PlayerGlassCutting : MonoBehaviour, IMinigame
{
    [SerializeField, Tooltip("Cut speed and aim tolerance")]
    private GlassCutterTuning tuning;

    private PlayerInputs input;
    private PlayerMinigame minigame;
    private GlassCutterView view;
    private MinigameScreen screen;

    private GlassCutterModel model;
    private GlassCutterInteraction targetGlass;

    public MinigameScreen Screen => screen;
    public IMinigameModel Model => model;

    // Free cursor: the cone follows the pointer
    public bool LocksCursor => false;
    public ConductFlags Conduct => ConductFlags.GlassCutting;

    private void Awake()
    {
        input = GetComponent<PlayerInputs>();
        minigame = GetComponent<PlayerMinigame>();

        if (tuning == null)
        {
            Debug.LogWarning($"{name} needs a GlassCutterTuning", this);
            enabled = false;
        }
    }

    public void Begin(GlassCutterInteraction glass)
    {
        if (!enabled || glass == null || minigame.IsPlaying || !FindView()) return;

        targetGlass = glass;
        model = new GlassCutterModel(tuning.CutSpeed);

        if (!minigame.Begin(this))
        {
            model = null;
            targetGlass = null;
            return;
        }

        view.Begin(model, tuning.CutClockwise);
    }

    public void Tick(float deltaTime)
    {
        bool aimed = view.TryGetAim(input.aim, tuning.MinAimDistance, out float angleToTangent);
        model.Tick(deltaTime, aimed && angleToTangent <= tuning.AlignmentTolerance, input.tension);
    }

    public void Complete() => targetGlass.OnCut();

    public void Stop()
    {
        view.End();
        model = null;
        targetGlass = null;
    }

    // The view lives in the scene's MinigameRig; found on first use, so the order of the two objects'
    // Awake calls does not matter
    private bool FindView()
    {
        if (view != null) return true;

        if (MinigameRig.Current != null)
            view = MinigameRig.Current.GetComponentInChildren<GlassCutterView>(true);

        if (view == null)
        {
            Debug.LogWarning($"{name} found no GlassCutterView in the scene's MinigameRig", this);
            enabled = false;
            return false;
        }

        // GlassCutterView requires a MinigameScreen beside it
        screen = view.GetComponent<MinigameScreen>();
        return true;
    }
}
