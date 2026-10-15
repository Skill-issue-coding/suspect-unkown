using TMPro;
using UnityEngine;

// One line of the job card's checklist: an item's name beside an empty box, which is ticked and the name
// struck through once the item is taken. Cloned once per required item by ObjectiveListHud
public class ObjectiveRow : MonoBehaviour
{
    [SerializeField, Tooltip("The item's name")]
    private TMP_Text label;

    [SerializeField, Tooltip("The filled, ticked box laid over the empty one; shown once the item is taken")]
    private GameObject doneMark;

    [SerializeField, Tooltip("Text color while the item is still to take")]
    private Color openColor = Color.white;

    [SerializeField, Tooltip("Text color once the item is taken")]
    private Color doneColor = new Color(1f, 1f, 1f, 0.5f);

    private void Awake()
    {
        if (label == null || doneMark == null)
        {
            Debug.LogWarning($"{name} needs a label and a done mark; assign it in JobHud.prefab", this);
            enabled = false;
        }
    }

    public void Show(string itemName, bool done)
    {
        if (!enabled) return;

        label.text = itemName;
        label.color = done ? doneColor : openColor;
        label.fontStyle = done ? FontStyles.Strikethrough : FontStyles.Normal;
        doneMark.SetActive(done);
    }
}
