using NUnit.Framework;

public class WitnessMergeTests
{
    [Test]
    public void Merge_AddsEvidence_FromBothWitnesses()
    {
        FeatureBelief a = new FeatureBelief(10);
        FeatureBelief b = new FeatureBelief(10);
        a.AddEvidence(3, 1f);
        b.AddEvidence(3, 1f);
        a.TryGetLeading(out _, out float alone);

        a.Merge(b);
        a.TryGetLeading(out int value, out float merged);

        Assert.AreEqual(3, value);
        Assert.Greater(merged, alone);
    }

    [Test]
    public void Merge_DisagreeingWitnesses_StrongerWins()
    {
        FeatureBelief a = new FeatureBelief(10);
        FeatureBelief b = new FeatureBelief(10);
        a.AddEvidence(2, 3f);
        b.AddEvidence(5, 1f);
        a.Merge(b);

        a.TryGetLeading(out int value, out _);
        Assert.AreEqual(2, value);
    }

    [Test]
    public void Merge_WithNothingLearned_ChangesNothing()
    {
        FeatureBelief a = new FeatureBelief(10);
        a.AddEvidence(4, 2f);
        a.TryGetLeading(out _, out float before);

        a.Merge(new FeatureBelief(10));
        a.TryGetLeading(out _, out float after);

        Assert.AreEqual(before, after, 1e-6f);
    }

    [Test]
    public void Confidence_MatchesSoftmax()
    {
        FeatureBelief a = new FeatureBelief(10);
        a.AddEvidence(1, 2f);
        a.TryGetLeading(out _, out float confidence);

        float expected = UnityEngine.Mathf.Exp(2f) / (UnityEngine.Mathf.Exp(2f) + 9f);
        Assert.AreEqual(expected, confidence, 1e-5f);
    }

    [Test]
    public void Fade_KeepsLeader_LowersConfidence()
    {
        FeatureBelief a = new FeatureBelief(10);
        a.AddEvidence(6, 3f);
        a.TryGetLeading(out _, out float before);

        a.Fade(0.5f);
        a.TryGetLeading(out int value, out float after);

        Assert.AreEqual(6, value);
        Assert.Less(after, before);
    }

    [Test]
    public void LongStare_DoesNotOverflow()
    {
        FeatureBelief a = new FeatureBelief(10);
        a.AddEvidence(1, 500f);
        a.TryGetLeading(out _, out float confidence);

        Assert.IsFalse(float.IsNaN(confidence));
        Assert.AreEqual(1f, confidence, 1e-5f);
    }

    [Test]
    public void Report_NoEvidence_SawNothing()
    {
        Assert.IsFalse(new WitnessReport().SawAnything);
    }
}
