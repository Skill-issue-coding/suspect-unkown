using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// The night map on the planning table: owns which target is selected and fills the
// briefing card. Only reacts while the map station is active, so the buildings cannot be
// clicked from across the room in daylight.
public class MapBoard : MonoBehaviour
{
    [Tooltip("Parent of the location labels, shown only at night")]
    [SerializeField] private GameObject labels;
    [Tooltip("Briefing card, hidden until a target is picked")]
    [SerializeField] private CanvasGroup card;
    [Tooltip("Card fields")]
    [SerializeField] private TMP_Text title, jobType, stars, security, take, profileNote;
    [Tooltip("Scene loaded by Plan Heist. Must be in the build profile's scene list")]
    [SerializeField] private string heistScene = "TestingScene";

    private MapLocation selected;

    public bool IsActive { get; private set; }
    public MapLocation Selected => selected;

    private void Awake()
    {
        if (card == null || title == null)
        {
            Debug.LogWarning($"{name} is missing its briefing card", this);
            enabled = false;
            return;
        }
        SetActive(false);
    }

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

        bool show = selected != null;
        card.alpha = show ? 1f : 0f;
        card.interactable = card.blocksRaycasts = show;
        if (!show) return;

        title.text = selected.DisplayName.ToUpperInvariant();
        jobType.text = selected.JobType;
        // Electrolize is a static ASCII atlas, so the difficulty meter is slashes, not stars
        stars.text = "<color=#F2B33D>" + new string('/', selected.Difficulty * 2) + "</color><color=#FFFFFF30>" + new string('/', (5 - selected.Difficulty) * 2) + "</color>";
        security.text = selected.Security;
        take.text = Wallet.Money(selected.EstimatedTake) + " <size=70%><color=#B8B8B8>dirty</color></size>";
        profileNote.text = selected.ProfileNote;
    }

    public void PlanHeist()
    {
        if (selected == null) return;
        if (!Application.CanStreamedLevelBeLoaded(heistScene))
        {
            Debug.LogWarning($"{name}: scene '{heistScene}' is not in the build scene list, so Plan Heist cannot load it", this);
            return;
        }
        SceneManager.LoadScene(heistScene);
    }
}
