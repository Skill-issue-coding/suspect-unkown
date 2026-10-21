using System;
using UnityEngine;
using System.Collections.Generic;

// One witness's belief about one property of one slot (a slot's style, or its color): a log-odds score per
// value, all starting at 0, so every value starts even. Each sighting adds evidence for the value seen;
// confidence in a value is its share of all scores (softmax). Independent witnesses merge by adding scores.
// Plain values, so a report can be saved as it is (concept 6.2: Bayesian, no machine learning)
[Serializable]
public class FeatureBelief
{
    [SerializeField] private float[] scores;    // index is value - 1: value 0 is Unknown and teaches nothing
    [SerializeField] private bool learned;

    public bool Learned => learned;

    public void AddEvidence(int value, float weight)
    {
        int i = value - 1;
        if (i < 0 || i >= scores.Length) return;    // Unknown, or a style above StyleCount
        scores[i] += weight;
        learned = true;
    }

    // possibleValues: how many values this property can take (10 colors, 3 styles)
    public FeatureBelief(int possibleValues) => scores = new float[Mathf.Max(1, possibleValues)];

    // False for a belief read from a file that does not fit this property (a hand-edited or outdated save)
    public bool Fits(int possibleValues) => scores != null && scores.Length == possibleValues;

    // Pulls every score toward 0, the even start, by factor (1 keeps all, 0 forgets all)
    public void Fade(float factor)
    {
        for (int i = 0; i < scores.Length; i++) scores[i] *= factor;
    }

    // Adds another witness's evidence: independent evidence adds in log-odds
    public void Merge(FeatureBelief other)
    {
        if (!other.learned) return;
        int count = Mathf.Min(scores.Length, other.scores.Length);
        for (int i = 0; i < count; i++) scores[i] += other.scores[i];
        learned = true;
    }

    public FeatureBelief Copy()
    {
        FeatureBelief copy = new FeatureBelief(scores.Length);
        Array.Copy(scores, copy.scores, scores.Length);
        copy.learned = learned;
        return copy;
    }

    // The value believed most, and how sure: 0 to 1. False while nothing has been learned
    public bool TryGetLeading(out int value, out float confidence)
    {
        value = 0;
        confidence = 0f;
        if (!learned) return false;

        int best = 0;
        for (int i = 1; i < scores.Length; i++)
            if (scores[i] > scores[best]) best = i;

        // Shifted by the best score, so a long stare cannot overflow Exp: the leader counts as 1
        float total = 0f;
        for (int i = 0; i < scores.Length; i++) total += Mathf.Exp(scores[i] - scores[best]);

        value = best + 1;
        confidence = 1f / total;
        return true;
    }

    // Returns all learned values (1-based) and their softmax confidence (0 to 1), highest first
    public List<(int value, float confidence)> GetRanked(float minScore = 0.05f)
    {
        List<(int value, float confidence)> results = new();
        if (!learned || scores == null || scores.Length == 0) return results;

        int best = 0;
        for (int i = 1; i < scores.Length; i++)
            if (scores[i] > scores[best]) best = i;

        // Ignore slots where even the highest score has negligible evidence
        if (scores[best] < minScore) return results;

        float total = 0f;
        for (int i = 0; i < scores.Length; i++)
            total += Mathf.Exp(scores[i] - scores[best]);

        for (int i = 0; i < scores.Length; i++)
        {
            if (scores[i] < minScore) continue;
            float conf = Mathf.Exp(scores[i] - scores[best]) / total;
            results.Add((i + 1, conf));
        }

        results.Sort((a, b) => b.confidence.CompareTo(a.confidence));
        return results;
    }
}
