using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

// One heist target on the planning-table city. Hovering draws a glowing outline around the
// building (grey while the job is locked), clicking selects it on the board. Picked up by the camera's PhysicsRaycaster
// through the BoxCollider, so no per-frame mouse ray is needed.
[RequireComponent(typeof(BoxCollider))]
public class MapLocation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Briefing")]
    [SerializeField, Tooltip("Name on the label and the briefing card")]
    private string displayName = "Target";

    [SerializeField, Tooltip("One line describing the kind of job")]
    private string jobType = "Smash and grab";

    [SerializeField, Range(1, 5), Tooltip("Difficulty, 1 to 5 stars")]
    private int difficulty = 1;

    [SerializeField, Tooltip("Security summary line")]
    private string security = "1 guard";

    [SerializeField, Tooltip("Optional. This job's security budget; with it, the saved completed runs decide whether the line reads 'Raised security'. Empty: the line above is shown as typed")]
    private SecurityBudget securityBudget;

    [SerializeField, Tooltip("Value of the loot known before the job, dollars")]
    private int knownTake = 0;

    [SerializeField, Tooltip("Estimated value on top of the known take, dollars. 0: none")]
    private int estimatedTake = 0;

    // Was profileNote: the attribute keeps the text already typed into the scene
    [SerializeField, FormerlySerializedAs("profileNote"), Tooltip("Tip shown under INTEL while the job is open")]
    private string intel = "";

    [Header("Presentation")]
    [SerializeField, Tooltip("Glowing outline, shown on hover and while selected")]
    private LineRenderer outline;

    [SerializeField, Tooltip("Outline material while the job is locked: grey, no glow. Empty: the outline keeps its glow")]
    private Material lockedOutline;

    [SerializeField, Tooltip("Board that owns the selection")]
    private MapBoard board;

    [Header("Job")]
    [SerializeField, Tooltip("Scene Start Heist loads. Must be in the build scene list. Empty: the job is not built yet and shows as coming soon")]
    private string sceneName = "";

    [SerializeField, Tooltip("What the save must hold before this job can be played. None: open from the start")]
    private ProgressUnlock requires = ProgressUnlock.None;

    [SerializeField, Tooltip("Card line while locked, saying how to unlock it")]
    private string lockedHint = "";

    private bool hovered;

    public string DisplayName => displayName;
    public string JobType => jobType;
    public int Difficulty => difficulty;

    // The card's security line: the typed text, or "Raised security" in red once the saved runs raise it
    public string SecurityLine(ActionSave actions)
    {
        if (securityBudget == null || !IsBuilt) return security;

        ActionRecord record = actions.Find(sceneName);
        int completedRuns = record != null ? record.completedRuns : 0;
        return securityBudget.IsRaised(completedRuns) ? "<color=#FF4D4D>Raised security</color>" : security;
    }

    public int KnownTake => knownTake;
    public int EstimatedTake => estimatedTake;
    public string Intel => intel;
    public string SceneName => sceneName;
    public ProgressUnlock Requires => requires;
    public string LockedHint => lockedHint;
    public bool IsBuilt => !string.IsNullOrEmpty(sceneName);

    private void Awake()
    {
        if (board == null)
        {
            Debug.LogWarning($"{name} has no map board assigned", this);
            enabled = false;
        }
        RefreshOutline();
    }

    // In Start, not Awake: the board loads the save in its Awake, and every Awake runs before any Start
    private void Start()
    {
        if (outline != null && lockedOutline != null && board.enabled && !board.IsOpen(this))
            outline.sharedMaterial = lockedOutline;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!enabled || !board.IsActive) return;
        hovered = true;
        RefreshOutline();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        RefreshOutline();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!enabled || !board.IsActive || eventData.button != PointerEventData.InputButton.Left) return;
        board.Select(this);
    }

    public void RefreshOutline()
    {
        if (outline != null) outline.enabled = hovered || (board != null && board.Selected == this);
    }
}
