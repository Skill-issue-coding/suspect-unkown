using UnityEngine;
using UnityEngine.EventSystems;

// One heist target on the planning-table city. Hovering draws a glowing outline around the
// building, clicking selects it on the board. Picked up by the camera's PhysicsRaycaster
// through the BoxCollider, so no per-frame mouse ray is needed.
[RequireComponent(typeof(BoxCollider))]
public class MapLocation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Briefing")]
    [Tooltip("Name on the label and the briefing card")]
    [SerializeField] private string displayName = "Target";
    [Tooltip("One line describing the kind of job")]
    [SerializeField] private string jobType = "Smash and grab";
    [Tooltip("Difficulty, 1 to 5 stars")]
    [Range(1, 5)]
    [SerializeField] private int difficulty = 1;
    [Tooltip("Security summary line")]
    [SerializeField] private string security = "1 guard";
    [Tooltip("Expected take, dirty money")]
    [SerializeField] private int estimatedTake = 1000;
    [Tooltip("What the police profile already holds that matters here")]
    [SerializeField] private string profileNote = "";

    [Header("Presentation")]
    [Tooltip("Glowing outline, shown on hover and while selected")]
    [SerializeField] private LineRenderer outline;
    [Tooltip("Board that owns the selection")]
    [SerializeField] private MapBoard board;

    private bool hovered;

    public string DisplayName => displayName;
    public string JobType => jobType;
    public int Difficulty => difficulty;
    public string Security => security;
    public int EstimatedTake => estimatedTake;
    public string ProfileNote => profileNote;

    private void Awake()
    {
        if (board == null)
        {
            Debug.LogWarning($"{name} has no map board assigned", this);
            enabled = false;
        }
        RefreshOutline();
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
