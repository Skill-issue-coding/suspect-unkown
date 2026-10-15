using System.Collections.Generic;

// The XP table from apartment-level.md 3.7, in one place: from a job's facts to the lines the debrief shows
// and their total. Pure logic with no state, so the heist and the menu always agree and it is unit-tested
public static class XpRules
{
    public const int PerObjective = 250;
    public const int LootDivisor = 10;
    public const int CleanGetaway = 150;

    // Bonus kinds the BONUS LOOT detail names one by one; more are summed up as "N items  $V" so the line fits
    public const int BonusKindsNamed = 2;

    // Indexed by WitnessClarity, so this relies on its declaration order: Unseen, Vague, Clear
    private static readonly int[] WitnessXp = { 300, 200, 100 };

    // Clears and fills lines, one per XP source, and returns their sum
    public static int Score(JobResult result, List<XpLine> lines)
    {
        lines.Clear();

        int objectives = 0, bonusValue = 0, bonusKinds = 0, bonusItems = 0;
        string objectiveNames = "", bonusNames = "";
        foreach (JobLoot loot in result.loot)
        {
            if (loot.objective)
            {
                objectives += loot.count;
                objectiveNames = Join(objectiveNames, loot.name, " and ");
            }
            else
            {
                bonusValue += loot.value;
                bonusKinds++;
                bonusItems += loot.count;
                string name = loot.count > 1 ? $"{loot.name} x{loot.count}" : loot.name;
                bonusNames = Join(bonusNames, $"{name}  ${loot.value}", ", ");
            }
        }

        if (bonusKinds > BonusKindsNamed) bonusNames = $"{bonusItems} items  ${bonusValue}";

        bool quiet = result.peakAlert < JobAlertLevel.Alarm;
        int witness = (int)result.witness;

        Add(lines, "OBJECTIVES", objectiveNames.Length > 0 ? objectiveNames : "None", objectives * PerObjective);
        Add(lines, "BONUS LOOT", bonusNames.Length > 0 ? bonusNames : "None", bonusValue / LootDivisor);
        Add(lines, "CLEAN GETAWAY", quiet ? "No alarm raised" : "Alarm raised", quiet ? CleanGetaway : 0);
        Add(lines, "WITNESS REPORT", WitnessDetail[witness], WitnessXp[witness]);

        int total = 0;
        foreach (XpLine line in lines) total += line.xp;
        return total;
    }

    // Indexed by WitnessClarity like WitnessXp
    private static readonly string[] WitnessDetail = { "No usable description", "Vague description", "Clear description" };

    private static string Join(string text, string part, string separator) =>
        text.Length == 0 ? part : text + separator + part;

    private static void Add(List<XpLine> lines, string label, string detail, int xp) =>
        lines.Add(new XpLine { label = label, detail = detail, xp = xp });
}