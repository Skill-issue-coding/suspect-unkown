using System;
using NUnit.Framework;

// R1: how well a worn outfit fits a police description (thrift shop §6). Each slot counts its color
// confidence when the color matches, half that when only the style differs, and 0 below the cut-off
public class RecognitionMatchTests
{
    private const float VagueAt = 0.2f;

    // Log-odds evidence per slot. 10 is a sure description (about 99.96 % over ten colors); 0.5 is a vague
    // one (about 15 %, under VagueAt)
    private const float Sure = 10f;
    private const float Vague = 0.5f;

    private static CharacterAppearance Outfit() => new CharacterAppearance
    {
        head = new OutfitFeature(1, AppearanceColor.Black),
        chest = new OutfitFeature(2, AppearanceColor.Red),
        legs = new OutfitFeature(1, AppearanceColor.Blue),
        feet = new OutfitFeature(3, AppearanceColor.White),
    };

    private static WitnessReport Describe(CharacterAppearance outfit, float weight)
    {
        WitnessReport report = new WitnessReport();
        foreach (FeatureSlot slot in (FeatureSlot[])Enum.GetValues(typeof(FeatureSlot)))
            report.AddEvidence(slot, outfit[slot], weight);
        return report;
    }

    [Test]
    public void SameOutfit_SureDescription_MatchesHigh()
    {
        WitnessReport report = Describe(Outfit(), Sure);

        Assert.Greater(report.Match(Outfit(), VagueAt), 0.95f);
    }

    [Test]
    public void DifferentChestColor_DropsThatSlot()
    {
        WitnessReport report = Describe(Outfit(), Sure);
        float full = report.Match(Outfit(), VagueAt);

        CharacterAppearance changed = Outfit();
        changed.chest = new OutfitFeature(2, AppearanceColor.Green);

        Assert.AreEqual(full * 0.75f, report.Match(changed, VagueAt), 1e-4f);
    }

    [Test]
    public void DifferentStyle_HalvesThatSlot()
    {
        WitnessReport report = Describe(Outfit(), Sure);
        float full = report.Match(Outfit(), VagueAt);

        CharacterAppearance changed = Outfit();
        changed.chest = new OutfitFeature(1, AppearanceColor.Red);

        Assert.AreEqual(full * 0.875f, report.Match(changed, VagueAt), 1e-4f);
    }

    [Test]
    public void VagueDescription_MatchesZero()
    {
        WitnessReport report = Describe(Outfit(), Vague);

        Assert.AreEqual(0f, report.Match(Outfit(), VagueAt));
    }

    [Test]
    public void EmptyDescription_MatchesZero()
    {
        Assert.AreEqual(0f, new WitnessReport().Match(Outfit(), VagueAt));
    }

    // Burned parts: color and style both match, believed at least WitnessReport.BurnedColorAt and BurnedStyleAt
    [Test]
    public void SureDescription_BurnsEveryPart()
    {
        WitnessReport report = Describe(Outfit(), Sure);

        Assert.AreEqual(4, report.BurnedCount(Outfit()));
    }

    [Test]
    public void DifferentStyle_DoesNotBurnThatPart()
    {
        WitnessReport report = Describe(Outfit(), Sure);

        CharacterAppearance changed = Outfit();
        changed.chest = new OutfitFeature(1, AppearanceColor.Red);

        Assert.AreEqual(3, report.BurnedCount(changed));
    }

    [Test]
    public void VagueDescription_BurnsNothing()
    {
        WitnessReport report = Describe(Outfit(), Vague);

        Assert.AreEqual(0, report.BurnedCount(Outfit()));
    }

    [Test]
    public void OnlyDescribedParts_Burn()
    {
        WitnessReport report = new WitnessReport();
        report.AddEvidence(FeatureSlot.Head, Outfit().head, Sure);
        report.AddEvidence(FeatureSlot.Chest, Outfit().chest, Sure);

        Assert.AreEqual(2, report.BurnedCount(Outfit()));
    }
}
