using System;

// Stage A of adaptive security (thriftshop-level.md 5.1): predicts, from the player's completed runs,
// which entrance they will use and how much of a run they will spend in each room. A first-order Markov
// chain over room changes, a Dirichlet over entrances, and the occupancy the two give when propagated
// forward. Pure and built once at load, so it is tested without a scene. The chain's diagonal stays 0:
// the job log never records staying in a room, so smoothing it would invent probability no run supports
public class ActionModel
{
    private readonly float[] transitions;
    private readonly float[] entrances;
    private readonly float[] occupancy;

    public int RoomCount { get; }
    public int EntranceCount => entrances.Length;

    // entranceRooms: the room each entrance opens into (JobLayout.EntranceRoom), -1 when unlisted
    public ActionModel(ActionRecord record, int[] entranceRooms, float alpha, int steps)
    {
        int n = record.rooms.Length;
        if (record.transitions.Length != n * n)
            throw new ArgumentException($"{record.location}: {record.transitions.Length} transitions for {n} rooms");
        if (entranceRooms.Length != record.entries.Length)
            throw new ArgumentException($"{record.location}: {entranceRooms.Length} entrance rooms for {record.entries.Length} entrances");
        if (alpha <= 0f) throw new ArgumentOutOfRangeException(nameof(alpha), "Smoothing must be above 0, or an unvisited room divides by 0");
        if (steps < 1) throw new ArgumentOutOfRangeException(nameof(steps));

        RoomCount = n;
        transitions = BuildTransitions(record.transitions, n, alpha);
        entrances = BuildEntrances(record.entries, alpha);
        occupancy = BuildOccupancy(entranceRooms, steps);
    }

    // Unchecked, like JobLog: callers loop below RoomCount and EntranceCount
    public float Transition(int from, int to) => transitions[from * RoomCount + to];
    public float EntranceProbability(int entrance) => entrances[entrance];
    public float Occupancy(int room) => occupancy[room];

    // P[i][j] = (C[i][j] + α) / Σ_{k≠i} (C[i][k] + α) for j ≠ i, P[i][i] = 0
    private static float[] BuildTransitions(float[] counts, int n, float alpha)
    {
        float[] p = new float[n * n];
        for (int i = 0; i < n; i++)
        {
            float row = (n - 1) * alpha;
            for (int k = 0; k < n; k++)
                if (k != i) row += counts[i * n + k];
        }
    }
}
