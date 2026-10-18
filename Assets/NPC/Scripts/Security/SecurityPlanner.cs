using System;
using System.Collections.Generic;

// Stage B of adaptive security (thriftshop-level.md 5.2): greedy maximum coverage over the authored
// candidate POIs, scored on the ActionModel's entrance probabilities and room occupancy. Deterministic:
// no sampling, ties to the lower index, so the same record always gives the same plan. The objective is
// submodular, so greedy is within (1 − 1/e) of the best set
public static class SecurityPlanner
{
    public static SecurityPlan Plan(ActionRecord record, ActionModel model, IReadOnlyList<PoiCandidate> candidates, float detection, SecurityTier tier)
    {
        int budget = Math.Max(0, Math.Min(tier.adaptivePois, candidates.Count));
        List<SecurityPick> picks = new List<SecurityPick>(budget);
        bool[] chosen = new bool[candidates.Count];

        // Chance each target is still missed by every chosen POI: Π(1 − d), starting at 1
        var roomMiss = Filled(model.RoomCount);
        var entranceMiss = Filled(model.EntranceCount);
        float firstGain = 0f;

        float[] standalone = new float[candidates.Count];
        for (int c = 0; c < candidates.Count; c++)
            standalone[c] = Gain(candidates[c], model, roomMiss, entranceMiss, detection);

        for (int k = 0; k < budget; k++)
        {
            int best = -1;
            float bestGain = 0f;
            for (int c = 0; c < candidates.Count; c++)
            {
                if (chosen[c]) continue;
                float gain = Gain(candidates[c], model, roomMiss, entranceMiss, detection);
                if (best < 0 || gain > bestGain) { best = c; bestGain = gain; }
            }

            chosen[best] = true;
            if (k == 0) firstGain = bestGain;
            float multiplier = firstGain > 0f ? 1f + bestGain / firstGain : 1f;
            picks.Add(new SecurityPick(best, bestGain, multiplier, Explain(candidates[best], record, model, multiplier)));
            Cover(candidates[best], roomMiss, entranceMiss, detection);
        }

        return new SecurityPlan(tier.guards, picks, standalone);
    }

    // How much Score(S) grows if this candidate joins: each covered target's weight × its remaining miss × d
    private static float Gain(PoiCandidate c, ActionModel model, float[] roomMiss, float[] entranceMiss, float d)
    {
        float gain = 0f;
        foreach (int r in c.rooms) gain += model.Occupancy(r) * roomMiss[r] * d;
        foreach (int e in c.entrances) gain += model.EntranceProbability(e) * entranceMiss[e] * d;
        return gain;
    }

    private static void Cover(PoiCandidate c, float[] roomMiss, float[] entranceMiss, float d)
    {
        foreach (int r in c.rooms) roomMiss[r] *= 1f - d;
        foreach (int e in c.entrances) entranceMiss[e] *= 1f - d;
    }

    private static float[] Filled(int length)
    {
        float[] a = new float[length];
        Array.Fill(a, 1f);
        return a;
    }

    // Names the candidate's strongest target in the model's own numbers. The record has decayed sums, not a
    // run history, so the line quotes probabilities rather than "used on 2 of 3 runs"
    private static string Explain(PoiCandidate c, ActionRecord record, ActionModel model, float multiplier)
    {
        int entrance = -1, room = -1;
        float best = -1f;

        foreach (int e in c.entrances)
        {
            float p = model.EntranceProbability(e);
            if (p > best) { best = p; entrance = e; }
        }

        foreach (int r in c.rooms)
        {
            float o = model.Occupancy(r);
            if (o > best) { best = o; room = r; entrance = -1; }
        }

        string reason =
            entrance >= 0 ? $"you come in through {record.entrances[entrance]} on {best:P0} of runs" :
            room >= 0 ? $"you spend {best:P0} of a run in {record.rooms[room]}" :
                            "it covers nothing";

        return $"Guards now check {c.name}: {reason}, drains {multiplier:0.0}× faster";
    }
}
