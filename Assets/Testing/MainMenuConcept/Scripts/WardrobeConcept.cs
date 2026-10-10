using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Concept wardrobe (concept 7.2 and 6.3) on top of the real player. The left panel is one
// "< part name >" row per PlayerCustomization category; the arrows call the player's own
// PreviousPart/NextPart, so the menu changes exactly what the game will load. The right panel
// is the police profile: which part the police believe the suspect wears and how sure they
// are. Wearing a part the profile has confirmed is flagged, because clothing is evidence.
// Profile data is inline because this is a mock-up; the real profile comes from the save.
public class WardrobeConcept : MonoBehaviour
{
    [Serializable]
    public class Row
    {
        [Tooltip("PlayerCustomization category this row drives, for example Head")]
        public string category;
        [Tooltip("Arrow buttons")]
        public Button previous, next;
        [Tooltip("Equipped part name and its position in the list")]
        public TMP_Text partLabel, countLabel;

        [Tooltip("Part name the police profile holds for this category, empty if unknown")]
        public string profilePart;
        [Tooltip("Profile confidence in that part, 0 to 1")]
        [Range(0f, 1f)] public float profileConfidence;
        [Tooltip("Profile row: part, confidence bar fill, status")]
        public TMP_Text profileLabel;
        public RectTransform profileBar;
        public TMP_Text profileStatus;
    }

    [Tooltip("The player in the wardrobe")]
    [SerializeField] private PlayerCustomization customization;
    [Tooltip("One row per category, top to bottom")]
    [SerializeField] private Row[] rows;
    [Tooltip("Shown when the outfit matches the profile")]
    [SerializeField] private TMP_Text warning;
    [Tooltip("Profile confidence above which a part counts as burned")]
    [Range(0f, 1f)]
    [SerializeField] private float burnedAt = 0.75f;

    private void Awake()
    {
        if (customization == null || rows == null || rows.Length == 0)
        {
            Debug.LogWarning($"{name} needs a PlayerCustomization and rows", this);
            enabled = false;
        }
    }

    private void Start()
    {
        foreach (Row row in rows)
        {
            Row r = row;
            r.previous.onClick.AddListener(() => { customization.PreviousPart(r.category); Refresh(); });
            r.next.onClick.AddListener(() => { customization.NextPart(r.category); Refresh(); });
        }

        // PlayerCustomization loads the saved look in its own Start, which may run after this one
        customization.LoadAppearance();
        Refresh();
    }

    private void Refresh()
    {
        int matches = 0;
        foreach (Row row in rows)
        {
            PlayerCustomization.CustomizationCategory category = Find(row.category);
            if (category == null || category.parts.Length == 0) continue;

            GameObject part = category.parts[category.currentIndex];
            string partName = part != null ? part.name : "";
            bool burned = partName == row.profilePart && row.profileConfidence >= burnedAt;
            if (burned) matches++;

            row.partLabel.text = burned ? $"{Pretty(partName)}  <color=#FF6B5B>BURNED</color>" : Pretty(partName);
            row.countLabel.text = $"{category.currentIndex + 1} / {category.parts.Length}";

            row.profileLabel.text = string.IsNullOrEmpty(row.profilePart)
                ? $"{row.category}: [?]"
                : $"{row.category}: [{Pretty(row.profilePart).ToUpperInvariant()}]";
            row.profileBar.anchorMax = new Vector2(Mathf.Max(0.02f, row.profileConfidence), 1f);
            row.profileStatus.text = StatusFor(row.profileConfidence);
        }

        if (warning != null)
            warning.text = matches == 0
                ? "<color=#8FD18B>Outfit does not match the suspect on file</color>"
                : $"<color=#FF6B5B>{matches} part{(matches > 1 ? "s" : "")} match the suspect on file. Witnesses will connect you.</color>";
    }

    private PlayerCustomization.CustomizationCategory Find(string categoryName)
    {
        foreach (PlayerCustomization.CustomizationCategory category in customization.categories)
            if (category.categoryName == categoryName) return category;
        return null;
    }

    // "Chest_002_Red" reads as "Style 2  Red"; anything else is shown as it is
    public static string Pretty(string partName)
    {
        string[] bits = partName.Split('_');
        if (bits.Length == 3 && int.TryParse(bits[1], out int style)) return $"Style {style}  {bits[2]}";
        return partName;
    }

    private static string StatusFor(float confidence)
    {
        if (confidence < 0.05f) return "<color=#9A9A9A>No evidence</color>";
        if (confidence < 0.45f) return "<color=#F2B33D>Low confidence</color>";
        if (confidence < 0.75f) return "<color=#F28C3D>Corroborated</color>";
        return "<color=#FF6B5B>Confirmed</color>";
    }
}
