using UnityEngine;

// The level curve, in one place for the heist and the menu: level 1 to 2 needs FirstLevel XP, each level
// after that LevelStep more. The save keeps only the XP total and the level is worked out from it, so the
// two can never disagree. Every level gained is worth one skill point
public static class Reputation
{
    private const int FirstLevel = 1000;
    private const int LevelStep = 250;

    public static int XpForLevel(int level) => FirstLevel + LevelStep * (level - 1);

    // Level for a total, and how far into that level the total reaches
    public static int LevelFor(int totalXp, out int xpIntoLevel)
    {
        int level = 1;
        int left = Mathf.Max(0, totalXp);

        // Pay for each level in turn until the next one costs more than is left
        while (left >= XpForLevel(level))
        {
            left -= XpForLevel(level);
            level++;
        }

        xpIntoLevel = left;
        return level;
    }
}
