using UnityEngine;

// The end of a reaction tree: weighs the outcomes for one kind of situation and samples one in
// proportion to its weight rather than taking the top score, so the same scene can play out
// differently between runs (concept 6.8) while an outcome weighted 0 never happens
[CreateAssetMenu(fileName = "ReactionLeaf", menuName = "Suspect Unknown/NPC/Reaction/Leaf")]
public class ReactionLeaf : ReactionNode
{
    [SerializeField, Tooltip("The outcomes this leaf can pick, each with its relative chance")]
    private ReactionWeight[] weights = { };

    public override ReactionLeaf Resolve(in ReactionContext context, int depth) => this;

    // The outcomes something carries out: the call on NpcDecisionWindow, the rest on CivilianBehaviour
    public static bool IsOutcome(CivilianActivity a) => a is CivilianActivity.Calling or CivilianActivity.Scared or CivilianActivity.Avoiding;

    // False when no outcome has weight. Chance is the picked outcome's share of the total, for the readout
    public bool TrySample(out CivilianActivity outcome, out float chance)
    {
        outcome = default;
        chance = 0f;

        float total = Total();
        if (total <= 0f) return false;

        // Each weight owns a stretch of a line of length total; the pick lands in one of them
        float pick = Random.value * total;
        foreach (ReactionWeight w in weights)
        {
            if (!IsOutcome(w.outcome) || w.weight <= 0f) continue;
            outcome = w.outcome;
            chance = w.weight / total;
            pick -= w.weight;
            if (pick < 0f) return true;
        }

        // Random.value can be exactly 1, so the pick can sit at the very end: keep the last weighted outcome
        return true;
    }

    // For the readout: each outcome's share of the leaf, such as "Scared 75%, Calling 25%". Builds a
    // string, so call it from debug drawing only, never from a per-frame gameplay path
    public string DescribeScores()
    {
        float total = Total();
        if (total <= 0f) return "no weights";

        var text = new System.Text.StringBuilder();
        foreach (ReactionWeight w in weights)
        {
            if (!IsOutcome(w.outcome)) continue;
            if (text.Length > 0) text.Append(", ");
            text.Append(w.outcome).Append(' ').Append((w.weight / total).ToString("P0"));
        }
        return text.ToString();
    }

    // Weight of the outcomes something carries out; anything else counts as 0
    private float Total()
    {
        float total = 0f;
        foreach (ReactionWeight w in weights)
            if (IsOutcome(w.outcome)) total += w.weight;
        return total;
    }

    private void OnValidate()
    {
        float total = 0f;
        foreach (ReactionWeight w in weights)
        {
            if (!IsOutcome(w.outcome))
                Debug.LogWarning($"{name} lists {w.outcome}, which no civilian carries out; it counts as 0", this);
            else
                total += w.weight;
        }

        if (total <= 0f)
            Debug.LogWarning($"{name} has no outcome with weight; a civilian reaching it falls back to a call", this);
    }
}
