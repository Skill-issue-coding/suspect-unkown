using UnityEngine;
using TMPro;
using UnityEngine.Serialization;

// The night map on the planning table: owns which target is selected and fills the
// briefing card. Only reacts while the map station is active, so the buildings cannot be
// clicked from across the room in daylight. Decides which jobs are open from the save,
// loaded once: the menu reloads after every job, so it is always current.
public class MapBoard : MonoBehaviour
{
    [Tooltip("Parent of the location labels, shown only at night")]
    [SerializeField] private GameObject labels;

    [Tooltip("Briefing card, hidden until a target is picked")]
    [SerializeField] private CanvasGroup card;

    [Tooltip("Card fields")]
    [SerializeField] private TMP_Text title, jobType, stars, security, take;

    // Was profileNote: the attribute keeps the card's reference to the label
    [Tooltip("INTEL line: the job's tip, or how to unlock it")]
    [SerializeField, FormerlySerializedAs("profileNote")] private TMP_Text intel;

    [Tooltip("Start Heist button, disabled while the selected job is locked")]
    [SerializeField] private MenuButtonFx startHeistButton;

    private MapLocation selected;
    private ProgressSave progress;
    private ActionSave actions;

    public bool IsActive { get; private set; }
    public MapLocation Selected => selected;

    private void Awake()
    {
        if (card == null || title == null || intel == null || startHeistButton == null)
        {
            Debug.LogWarning($"{name} is missing its briefing card, intel line or start heist button", this);
            enabled = false;
            return;
        }
        progress = ProgressStore.Load();
        actions = ActionStore.Load();
        SetActive(false);
    }

    // Open when the job exists and the save holds what it needs
    public bool IsOpen(MapLocation location) => location.IsBuilt && progress.Has(location.Requires);

    public void SetActive(bool on)
    {
        IsActive = on;
        if (labels != null) labels.SetActive(on);
        if (!on) Select(null);
    }

    public void Select(MapLocation location)
    {
        MapLocation previous = selected;
        selected = location;
        if (previous != null) previous.RefreshOutline();
        if (selected != null) selected.RefreshOutline();

        // The OP shows the item that led to the selected target, once it was stolen. Has, not IsOpen: an
        // unbuilt job still shows the item that unlocked it
        if (previous != null && previous.StoryItem != null) previous.StoryItem.Hide();
        if (selected != null && selected.StoryItem != null && progress.Has(selected.Requires)) selected.StoryItem.Show();

        bool show = selected != null;
        card.alpha = show ? 1f : 0f;
        card.interactable = card.blocksRaycasts = show;
        if (!show) return;

        title.text = selected.DisplayName.ToUpperInvariant();
        jobType.text = selected.JobType;
        // Electrolize is a static ASCII atlas, so the difficulty meter is slashes, not stars
        stars.text = "<color=#F2B33D>" + new string('/', selected.Difficulty * 2) + "</color><color=#FFFFFF30>" + new string('/', (5 - selected.Difficulty) * 2) + "</color>";
        security.text = selected.SecurityLine(actions);
        // Either part may be missing: a job with no known loot shows only the estimate
        string known = selected.KnownTake > 0 ? Wallet.Money(selected.KnownTake) + " <size=70%><color=#B8B8B8>known</color></size>" : "";
        string estimate = selected.EstimatedTake > 0 ? Wallet.Money(selected.EstimatedTake) + " <size=70%><color=#B8B8B8>estimated</color></size>" : "";
        take.text = known.Length > 0 && estimate.Length > 0 ? known + " + " + estimate : known.Length == 0 && estimate.Length == 0 ? "?" : known + estimate;

        bool open = IsOpen(selected);
        startHeistButton.SetLocked(!open);
        intel.text = open ? selected.Intel
            : "<color=#F2B33D>LOCKED</color>  " + (selected.IsBuilt ? selected.LockedHint : "Coming soon");
    }

    // Connected to the Start Heist button's On Click
    public void StartHeist()
    {
        if (selected == null || !IsOpen(selected)) return;
        // Warns by itself when the scene is not in the build scene list
        SceneTransition.Load(selected.SceneName);
    }
}
