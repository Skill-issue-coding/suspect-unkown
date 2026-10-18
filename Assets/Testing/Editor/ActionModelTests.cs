using NUnit.Framework;

// Stage A of adaptive security (thriftshop-level.md S3): the chain, the entrance Dirichlet and the
// occupancy on a small layout. Street plus three rooms; entrance 0 opens into room 1, entrance 1 into room 2
public class ActionModelTests
{
    private const int Rooms = 4;
    private const float Alpha = 0.5f;
    private const int Steps = 6;
    private const float Tolerance = 1e-5f;
    private static readonly int[] EntranceRooms = { 1, 2 };

    private static ActionRecord Record() => new ActionRecord
    {
        location = "Test",
        rooms = new string[Rooms],
        entrances = new string[EntranceRooms.Length],
        transitions = new float[Rooms * Rooms],
        entries = new float[EntranceRooms.Length],
    };

    private static ActionModel Model(ActionRecord record) => new ActionModel(record, EntranceRooms, Alpha, Steps);

    [Test]
    public void Rows_SumToOne()
    {
        ActionRecord record = Record();
        record.transitions[0 * Rooms + 2] = 3f;
        record.transitions[2 * Rooms + 3] = 1.8f;
        ActionModel model = Model(record);

        for (int i = 0; i < Rooms; i++)
        {
            float sum = 0f;
            for (int j = 0; j < Rooms; j++) sum += model.Transition(i, j);
            Assert.AreEqual(1f, sum, Tolerance, $"row {i}");
        }
    }

    [Test]
    public void UnseenTransitions_AboveZero()
    {
        ActionRecord record = Record();
        record.transitions[0 * Rooms + 2] = 3f;
        ActionModel model = Model(record);

        for (int j = 1; j < Rooms; j++)
            Assert.Greater(model.Transition(0, j), 0f, $"street to {j}");
    }

    [Test]
    public void Diagonal_IsZero()
    {
        ActionModel model = Model(Record());

        for (int i = 0; i < Rooms; i++)
            Assert.AreEqual(0f, model.Transition(i, i));
    }

    [Test]
    public void EmptyRecord_IsUniform()
    {
        ActionModel model = Model(Record());

        Assert.AreEqual(1f / 3f, model.Transition(0, 1), Tolerance);
        Assert.AreEqual(0.5f, model.EntranceProbability(0), Tolerance);
    }

    [Test]
    public void MostUsedEntrance_MostLikely()
    {
        ActionRecord record = Record();
        record.entries[1] = 3f;
        ActionModel model = Model(record);

        Assert.Greater(model.EntranceProbability(1), model.EntranceProbability(0));
    }

    [Test]
    public void Occupancy_SumsToOne_WithoutStreet()
    {
        ActionRecord record = Record();
        record.entries[1] = 2f;
        record.transitions[0 * Rooms + 2] = 2f;
        record.transitions[2 * Rooms + 3] = 1f;
        ActionModel model = Model(record);

        Assert.AreEqual(0f, model.Occupancy(0));
        float sum = 0f;
        for (int r = 1; r < Rooms; r++) sum += model.Occupancy(r);
        Assert.AreEqual(1f, sum, Tolerance);
    }

    [Test]
    public void Occupancy_FavoursRoomBehindUsedEntrance()
    {
        ActionRecord record = Record();
        record.entries[1] = 3f;
        record.transitions[0 * Rooms + 2] = 3f;
        ActionModel model = Model(record);

        Assert.Greater(model.Occupancy(2), model.Occupancy(1));
        Assert.Greater(model.Occupancy(2), model.Occupancy(3));
    }
}
