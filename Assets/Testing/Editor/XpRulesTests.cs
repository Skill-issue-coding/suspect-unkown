using NUnit.Framework;
using System.Collections.Generic;

// XpRules against the table in apartment-level.md 3.7: the perfect apartment, each bonus missed on its own,
// the witness tiers, rounding, and reusing the caller's list
public class XpRulesTests
{
    private readonly List<XpLine> lines = new();

    // Both objectives, $180 cash in six pickups, never past Suspicious, unseen: the 968 XP run
    private static JobResult PerfectApartment() => new JobResult
    {
        job = "Apartment",
        loot = new List<JobLoot>
        {
            new JobLoot { name = "Laptop", count = 1, value = 400, objective = true },
            new JobLoot { name = "Cellphone", count = 1, value = 150, objective = true },
            new JobLoot { name = "Cash", count = 6, value = 180 },
        },
        peakAlert = JobAlertLevel.Suspicious,
        witness = WitnessClarity.Unseen,
    };

    [Test]
    public void PerfectApartment_Gives968_InFourLines()
    {
        int total = XpRules.Score(PerfectApartment(), lines);

        Assert.AreEqual(968, total);
        Assert.AreEqual(4, lines.Count);
        Assert.AreEqual(500, lines[0].xp);
        Assert.AreEqual(18, lines[1].xp);
        Assert.AreEqual(150, lines[2].xp);
        Assert.AreEqual(300, lines[3].xp);
        Assert.AreEqual("Laptop and Cellphone", lines[0].detail);
    }

    [Test]
    public void Alarm_LosesCleanGetaway_ButKeepsTheLine()
    {
        JobResult result = PerfectApartment();
        result.peakAlert = JobAlertLevel.Alarm;

        int total = XpRules.Score(result, lines);

        Assert.AreEqual(818, total);
        Assert.AreEqual(4, lines.Count);
        Assert.AreEqual(0, lines[2].xp);
        Assert.AreEqual("Alarm raised", lines[2].detail);
    }

    [TestCase(WitnessClarity.Unseen, 300)]
    [TestCase(WitnessClarity.Vague, 200)]
    [TestCase(WitnessClarity.Clear, 100)]
    public void WitnessTiers_ScoreAsTheTable(WitnessClarity clarity, int expected)
    {
        JobResult result = PerfectApartment();
        result.witness = clarity;

        XpRules.Score(result, lines);

        Assert.AreEqual(expected, lines[3].xp);
    }

    [Test]
    public void NoBonusLoot_ShowsNone_AtZero()
    {
        JobResult result = PerfectApartment();
        result.loot.RemoveAt(2);

        XpRules.Score(result, lines);

        Assert.AreEqual(0, lines[1].xp);
        Assert.AreEqual("None", lines[1].detail);
    }

    [Test]
    public void BonusValue_RoundsDown()
    {
        JobResult result = PerfectApartment();
        result.loot[2] = new JobLoot { name = "Cash", count = 6, value = 189 };

        XpRules.Score(result, lines);

        Assert.AreEqual(18, lines[1].xp);
    }

    [Test]
    public void ManyBonusKinds_AreSummedUp_SoTheLineFits()
    {
        JobResult result = PerfectApartment();
        result.loot.Add(new JobLoot { name = "Watch", count = 1, value = 220 });
        result.loot.Add(new JobLoot { name = "Perfume", count = 2, value = 90 });

        XpRules.Score(result, lines);

        Assert.AreEqual("9 items  $490", lines[1].detail);
        Assert.AreEqual(49, lines[1].xp);
    }

    [Test]
    public void ReusedList_IsClearedFirst()
    {
        XpRules.Score(PerfectApartment(), lines);
        XpRules.Score(PerfectApartment(), lines);

        Assert.AreEqual(4, lines.Count);
    }
}
