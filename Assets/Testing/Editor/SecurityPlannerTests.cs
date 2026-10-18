using NUnit.Framework;

// Stage B of adaptive security (thriftshop-level.md S4): the budget table, the greedy picks, ties,
// determinism and the drain multipliers. Same layout as ActionModelTests: street plus three rooms,
// entrance 0 opens into room 1, entrance 1 into room 2
public class SecurityPlannerTests
{
    private const int Rooms = 4;
    private const float Alpha = 0.5f;
    private const int Steps = 6;
    private const float Detection = 0.6f;
    private static readonly int[] EntranceRooms = { 1, 2 };

    private static readonly PoiCandidate[] Candidates =
    {
        new PoiCandidate("Front door", new int[0], new[] { 0 }),
        new PoiCandidate("Side door",  new int[0], new[] { 1 }),
        new PoiCandidate("Room 3",     new[] { 3 }, new int[0]),
        new PoiCandidate("Room 2",     new[] { 2 }, new int[0]),
    };

    private static readonly SecurityTier[] ThriftShopTiers =
    {
        new SecurityTier { fromCompletedRuns = 0, guards = 1, adaptivePois = 0 },
        new SecurityTier { fromCompletedRuns = 1, guards = 1, adaptivePois = 1 },
        new SecurityTier { fromCompletedRuns = 2, guards = 2, adaptivePois = 2 },
    };

    private static ActionRecord Record(int completedRuns) => new ActionRecord
    {
        location = "Test",
        completedRuns = completedRuns,
        rooms = new[] { "Street", "Room 1", "Room 2", "Room 3" },
        entrances = new[] { "Front", "Side" },
        transitions = new float[Rooms * Rooms],
        entries = new float[EntranceRooms.Length],
    };

    // The record as the player would leave it after runs through the side door: in, then into room 2
    private static ActionRecord SideDoorRecord(int completedRuns, float count)
    {
        ActionRecord record = Record(completedRuns);
        record.entries[1] = count;
        record.transitions[0 * Rooms + 2] = count;
        return record;
    }

    private static SecurityPlan Plan(ActionRecord record) =>
        Plan(record, SecurityTier.For(ThriftShopTiers, record.completedRuns));

    private static SecurityPlan Plan(ActionRecord record, SecurityTier tier) =>
        SecurityPlanner.Plan(record, new ActionModel(record, EntranceRooms, Alpha, Steps), Candidates, Detection, tier);

    [TestCase(0, 1, 0)]
    [TestCase(1, 1, 1)]
    [TestCase(2, 2, 2)]
    [TestCase(5, 2, 2)]
    public void Tier_FollowsCompletedRuns(int runs, int guards, int adaptivePois)
    {
        SecurityTier tier = SecurityTier.For(ThriftShopTiers, runs);

        Assert.AreEqual(guards, tier.guards);
        Assert.AreEqual(adaptivePois, tier.adaptivePois);
    }

    [Test]
    public void ZeroRuns_PicksNothing()
    {
        SecurityPlan plan = Plan(Record(0));

        Assert.AreEqual(0, plan.Picks.Count);
        Assert.AreEqual(1, plan.Guards);
    }

    [Test]
    public void OneRun_PickMostUsedEntrance()
    {
        SecurityPlan plan = Plan(SideDoorRecord(1, 1f));

        Assert.AreEqual(1, plan.Picks.Count);
        Assert.AreEqual(1, plan.Picks[0].candidate);
        StringAssert.Contains("Side door", plan.Picks[0].explanation);
    }

    [Test]
    public void TwoRuns_PickTwoDifferent()
    {
        SecurityPlan plan = Plan(SideDoorRecord(2, 1.8f));

        Assert.AreEqual(2, plan.Guards);
        Assert.AreEqual(2, plan.Picks.Count);
        Assert.AreNotEqual(plan.Picks[0].candidate, plan.Picks[1].candidate);
    }

    [Test]
    public void SameRecord_SamePlan()
    {
        SecurityPlan a = Plan(SideDoorRecord(2, 1.8f));
        SecurityPlan b = Plan(SideDoorRecord(2, 1.8f));

        Assert.AreEqual(a.Picks.Count, b.Picks.Count);
        for (int i = 0; i < a.Picks.Count; i++)
        {
            Assert.AreEqual(a.Picks[i].candidate, b.Picks[i].candidate);
            Assert.AreEqual(a.Picks[i].gain, b.Picks[i].gain);
        }
    }

    [Test]
    public void FirstPick_DrainsTwiceAsFast()
    {
        SecurityPlan plan = Plan(SideDoorRecord(2, 1.8f));

        Assert.AreEqual(2f, plan.Picks[0].decayMultiplier);
        Assert.Greater(plan.Picks[1].decayMultiplier, 1f);
        Assert.LessOrEqual(plan.Picks[1].decayMultiplier, 2f);
    }

    [Test]
    public void Ties_GoToLowerIndex()
    {
        SecurityPlan plan = Plan(Record(1));

        Assert.AreEqual(0, plan.Picks[0].candidate);
    }

    [Test]
    public void Tier_SetsGuardsAndPicks()
    {
        // A bigger level: 7 guards, 3 adaptive POIs, independent of the thrift shop's table
        SecurityTier tier = new SecurityTier { fromCompletedRuns = 0, guards = 7, adaptivePois = 3 };

        SecurityPlan plan = Plan(SideDoorRecord(1, 1f), tier);

        Assert.AreEqual(7, plan.Guards);
        Assert.AreEqual(3, plan.Picks.Count);
    }

    [Test]
    public void Tier_MorePoisThanCandidates_PicksAllCandidates()
    {
        SecurityTier tier = new SecurityTier { fromCompletedRuns = 0, guards = 1, adaptivePois = 10 };

        SecurityPlan plan = Plan(SideDoorRecord(1, 1f), tier);

        Assert.AreEqual(Candidates.Length, plan.Picks.Count);
    }

    [Test]
    public void Tier_NegativePois_PicksNothing()
    {
        SecurityTier tier = new SecurityTier { fromCompletedRuns = 0, guards = 1, adaptivePois = -1 };

        SecurityPlan plan = Plan(SideDoorRecord(1, 1f), tier);

        Assert.AreEqual(0, plan.Picks.Count);
    }

    [Test]
    public void StandaloneGains_CoverEveryCandidate_EvenWithNoPicks()
    {
        SecurityPlan plan = Plan(Record(0));

        Assert.AreEqual(0, plan.Picks.Count);
        Assert.AreEqual(Candidates.Length, plan.StandaloneGains.Count);
    }

    [Test]
    public void FirstPick_HasTheLargestStandaloneGain()
    {
        SecurityPlan plan = Plan(SideDoorRecord(2, 1.8f));
        int first = plan.Picks[0].candidate;

        for (int c = 0; c < plan.StandaloneGains.Count; c++)
            Assert.LessOrEqual(plan.StandaloneGains[c], plan.StandaloneGains[first]);
        Assert.AreEqual(plan.StandaloneGains[first], plan.Picks[0].gain, 1e-6f);
    }
}
