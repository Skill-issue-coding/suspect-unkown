using UnityEngine;

// The job card's checklist: one row per required item in JobObjectives, ticked as each is taken. Each
// level requires different items, so the rows are cloned from a template once in Start and the card grows
// to fit them; after that they change only on JobObjectives.Changed
public class ObjectiveListHud : MonoBehaviour
{
    [SerializeField, Tooltip("The level's objectives. One row per required item")]
    private JobObjectives objectives;

    [SerializeField, Tooltip("Inactive row cloned once per required item, at its own position and below")]
    private ObjectiveRow template;

    [SerializeField, Tooltip("The job card panel; its height grows to fit the rows")]
    private RectTransform card;

    [SerializeField, Tooltip("Pixels from the top of one row to the top of the next")]
    private float rowSpacing = 30f;

    [SerializeField, Tooltip("Pixels of panel left below the last row")]
    private float bottomPadding = 18f;

    private ObjectiveRow[] rows;
    private ObjectiveRow escapeRow;

    Vector2 origin;

    private void Awake()
    {
        if (objectives == null || template == null || card == null)
        {
            Debug.LogWarning($"{name} needs objectives, a row template and the card", this);
            enabled = false;
        }
    }

    // Guarded: disabling in Awake still calls OnDisable, with objectives missing
    private void OnEnable()
    {
        if (objectives != null) objectives.Changed += Refresh;
    }

    private void OnDisable()
    {
        if (objectives != null) objectives.Changed -= Refresh;
    }

    private void Start()
    {
        var templateRect = (RectTransform)template.transform;
        origin = templateRect.anchoredPosition;

        rows = new ObjectiveRow[objectives.Required];
        for (int i = 0; i < rows.Length; i++)
        {
            ObjectiveRow row = Instantiate(template, templateRect.parent);
            row.name = $"Objective{i + 1}";
            ((RectTransform)row.transform).anchoredPosition = origin + Vector2.down * (rowSpacing * i);
            row.gameObject.SetActive(true);
            rows[i] = row;
        }

        escapeRow = Instantiate(template, templateRect.parent);
        escapeRow.name = "EscapeObjective";
        ((RectTransform)escapeRow.transform).anchoredPosition = origin + Vector2.down * (rowSpacing * rows.Length);
        escapeRow.gameObject.SetActive(false);

        Refresh();
    }

    private void Refresh()
    {
        if (rows == null) return;

        for (int i = 0; i < rows.Length; i++)
        {
            LootItem item = objectives.RequiredItem(i);
            rows[i].Show(item != null ? item.DisplayName : "?", objectives.IsTaken(i));
        }

        bool readyToEscape = objectives.IsComplete;
        if (escapeRow != null)
        {
            escapeRow.gameObject.SetActive(readyToEscape);
            if (readyToEscape) 
            {
                escapeRow.Show("Return to the escape van", false); 
            }
        }

        // The rows hang down from the template's position, so the card ends just below the last one
        int visibleRowsCount = readyToEscape ? rows.Length + 1 : rows.Length;
        float rowsHeight = visibleRowsCount == 0 ? 0f : rowSpacing * (visibleRowsCount - 1) + ((RectTransform)template.transform).rect.height;
        card.sizeDelta = new Vector2(card.sizeDelta.x, -origin.y + rowsHeight + bottomPadding);
    }
}
