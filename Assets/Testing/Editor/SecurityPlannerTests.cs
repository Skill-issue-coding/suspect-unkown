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
        SecurityPlanner.Plan(record, new ActionModel(record, EntranceRooms, Alpha, Steps), Candidates, Detection);

    [TestCase(0, 1, 0)]
    [TestCase(1, 1, 1)]
    [TestCase(2, 2, 2)]
    [TestCase(5, 2, 2)]
    public void Budget_FollowsCompletedRuns(int runs, int guards, int adaptivePois)
    {
        Assert.AreEqual(guards, SecurityPlanner.GuardCount(runs));
        Assert.AreEqual(adaptivePois, SecurityPlanner.AdaptivePoiCount(runs));
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
}
