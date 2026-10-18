using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WardrobeConcept : MonoBehaviour
{
    [Serializable]
    public class Row
    {
        [Tooltip("PlayerCustomization category this row drives, for example Head")]
        public string category;
        [Tooltip("Matching FeatureSlot in the WitnessReport (Head, Chest, Legs, Feet)")]
        public FeatureSlot slot;
        [Tooltip("Arrow buttons")]
        public Button previous, next;
        [Tooltip("Equipped part name and its position in the list")]
        public TMP_Text partLabel, countLabel;
        [Tooltip("Multiline text on the police canvas for this category")]
        public TMP_Text profileLabel;
    }

    [Tooltip("The player in the wardrobe")]
    [SerializeField] private PlayerCustomization customization;
    [Tooltip("One row per category, top to bottom")]
    [SerializeField] private Row[] rows;
    [Tooltip("Shown in red at the bottom when equipped parts are on the police board")]
    [SerializeField] private TMP_Text warning;
    [Tooltip("Button to wipe police.json memory")]
    [SerializeField] private Button identityResetButton;
    [Tooltip("How many known pieces to show per category (2 or 3 based on space)")]
    [Range(1, 3)]
    [SerializeField] private int maxPiecesPerSlot = 3;

    private WitnessReport record;

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

        if (identityResetButton != null)
            identityResetButton.onClick.AddListener(ResetIdentity);

        // Load directly from police.json
        record = PoliceStore.Load();
        Refresh();
    }

    public void ResetIdentity()
    {
        PoliceStore.Delete();
        record = PoliceStore.Load(); // Returns empty WitnessReport
        Refresh();
    }

    private void Refresh()
    {
        if (record == null) record = PoliceStore.Load();

        int matches = 0;
        StringBuilder sb = new StringBuilder();

        foreach (Row row in rows)
        {
            PlayerCustomization.CustomizationCategory category = Find(row.category);
            if (category == null || category.parts.Length == 0) continue;

            GameObject part = category.parts[category.currentIndex];
            string partName = part != null ? part.name : "";

            var knownPieces = record.DescribeTop(row.slot, maxPiecesPerSlot);

            // Check if the currently active piece in this slot is shown on the police board
            bool onBoard = false;
            if (TryParsePart(partName, out int equippedStyle, out AppearanceColor equippedColor))
            {
                foreach (var piece in knownPieces)
                {
                    if (piece.feature.style == equippedStyle && piece.feature.color == equippedColor)
                    {
                        onBoard = true;
                        break;
                    }
                }
            }

            if (onBoard) matches++;

            row.partLabel.text = onBoard ? $"{Pretty(partName)}  <color=#FF6B5B>BURNED</color>" : Pretty(partName);
            row.countLabel.text = $"{category.currentIndex + 1} / {category.parts.Length}";

            // Format the Police Profile text for this category
            if (row.profileLabel != null)
            {
                sb.Clear();

                if (knownPieces.Count == 0)
                {
                    sb.Append("[?]");
                }
                else
                {
                    for (int i = 0; i < knownPieces.Count; i++)
                    {
                        var p = knownPieces[i];
                        int pct = Mathf.Max(1, Mathf.RoundToInt(p.confidence * 100f));
                        sb.Append($"[Style {p.feature.style} {p.feature.color}] - {pct}%");
                        if (i < knownPieces.Count - 1) sb.AppendLine();
                    }
                }

                row.profileLabel.text = sb.ToString();
            }
        }

        // Red warning text at the bottom showing how many active pieces are on the police board
        if (warning != null)
        {
            warning.text = matches == 0
                ? "<color=#8FD18B>Outfit does not match the suspect on file</color>"
                : $"<color=#FF6B5B>{matches} active piece{(matches > 1 ? "s" : "")} currently on the police board. Witnesses will connect you.</color>";
        }
    }

    private PlayerCustomization.CustomizationCategory Find(string categoryName)
    {
        foreach (PlayerCustomization.CustomizationCategory category in customization.categories)
            if (category.categoryName == categoryName) return category;
        return null;
    }

    private static bool TryParsePart(string partName, out int style, out AppearanceColor color)
    {
        style = 0;
        color = default;
        if (string.IsNullOrEmpty(partName)) return false;

        string[] bits = partName.Split('_');
        return bits.Length == 3
            && int.TryParse(bits[1], out style)
            && Enum.TryParse(bits[2], true, out color);
    }

    // "Chest_002_Red" reads as "Style 2  Red"; anything else is shown as it is
    public static string Pretty(string partName)
    {
        string[] bits = partName.Split('_');
        if (bits.Length == 3 && int.TryParse(bits[1], out int style)) return $"Style {style}  {bits[2]}";
        return partName;
    }
}