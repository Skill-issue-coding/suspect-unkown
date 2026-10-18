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

            for (int j = 0; j < n; j++)
                p[i * n + j] = j == i ? 0f : (counts[i * n + j] + alpha) / row;
        }

        return p;
    }

    // p(e) = (entries[e] + α) / Σ (entries + α)
    private static float[] BuildEntrances(float[] entries, float alpha)
    {
        float total = entries.Length * alpha;
        foreach (float count in entries) total += count;

        float[] p = new float[entries.Length];
        for (int e = 0; e < entries.Length; e++)
            p[e] = (entries[e] + alpha) / total;
        return p;
    }

    // Seed from the entrances, propagate through P, average the steps, then leave the street out
    private float[] BuildOccupancy(int[] entranceRooms, int steps)
    {
        int n = RoomCount;
        float[] result = new float[n];
        float[] current = new float[n];
        float[] next = new float[n];

        for (int e = 0; e < entranceRooms.Length; e++)
            if ((uint)entranceRooms[e] < (uint)n) current[entranceRooms[e]] += entrances[e];

        for (int t = 0; t < steps; t++)
        {
            for (int r = 0; r < n; r++) result[r] += current[r];
            if (t == steps - 1) break;

            Array.Clear(next, 0, n);
            for (int i = 0; i < n; i++)
            {
                if (current[i] == 0f) continue;
                for (int j = 0; j < n; j++)
                    next[j] += current[i] * transitions[i * n + j];
            }
            (current, next) = (next, current);
        }

        // Guards stay inside, so the street is no place to cover
        result[0] = 0f;
        float sum = 0f;
        for (int r = 1; r < n; r++) sum += result[r];
        if (sum > 0f)
            for (int r = 1; r < n; r++) result[r] /= sum;
        return result;
    }
}
