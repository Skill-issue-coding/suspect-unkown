using System;
using UnityEngine;

// Which colors a poor look mistakes for which, as data: for each true color, the colors it can be
// recalled as and how likely each is. One asset shared by every slot (black reads as grey on a jacket and
// on shoes alike). A color with no row is mistaken for any other color, evenly (concept 6.2)
[CreateAssetMenu(fileName = "ColorConfusion", menuName = "Suspect Unknown/Witness/Color Confusion")]
public class ConfusionMatrix : ScriptableObject
{
    [Serializable]
    public struct Mistake
    {
        [Tooltip("The color remembered instead")]
        public AppearanceColor recalledAs;

        [Min(0f), Tooltip("Relative chance among this row's mistakes")]
        public float weight;
    }

    [Serializable]
    public struct Row
    {
        [Tooltip("The color actually worn")]
        public AppearanceColor color;

        [Tooltip("What a poor look may remember instead")]
        public Mistake[] mistakes;
    }

    [SerializeField, Tooltip("One row per color that has typical mistakes")]
    private Row[] rows;

    // A plausible wrong color for one that was really worn
    public AppearanceColor MistakeFor(AppearanceColor truth)
    {
        if (rows != null)
        {
            foreach (Row row in rows)
            {
                if (row.color != truth || row.mistakes == null || row.mistakes.Length == 0) continue;

                float total = 0f;
                foreach (Mistake m in row.mistakes) total += m.weight;
                float pick = UnityEngine.Random.value * total;
                foreach (Mistake m in row.mistakes)
                {
                    pick -= m.weight;
                    if (pick <= 0f) return m.recalledAs;
                }
            }
        }

        // No row: any other color, evenly (index 0 is Unknown, never chosen)
        int count = Enum.GetValues(typeof(AppearanceColor)).Length;
        int other = UnityEngine.Random.Range(1, count - 1);
        return (AppearanceColor)(other >= (int)truth ? other + 1 : other);
    }
}
