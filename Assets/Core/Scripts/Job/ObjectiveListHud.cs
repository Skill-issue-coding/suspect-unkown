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
        Vector2 origin = templateRect.anchoredPosition;

        rows = new ObjectiveRow[objectives.Required];
        for (int i = 0; i < rows.Length; i++)
        {
            ObjectiveRow row = Instantiate(template, templateRect.parent);
            row.name = $"Objective{i + 1}";
            ((RectTransform)row.transform).anchoredPosition = origin + Vector2.down * (rowSpacing * i);
            row.gameObject.SetActive(true);
            rows[i] = row;
        }

        // The rows hang down from the template's position, so the card ends just below the last one
        float rowsHeight = rows.Length == 0 ? 0f : rowSpacing * (rows.Length - 1) + templateRect.rect.height;
        card.sizeDelta = new Vector2(card.sizeDelta.x, -origin.y + rowsHeight + bottomPadding);

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
    }
}
