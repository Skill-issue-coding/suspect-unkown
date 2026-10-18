using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The shared security budget (thriftshop-level.md S7): the tier lookup the level uses and the "raised"
// test the main menu uses. The tiers field is private, so each test fills it through a SerializedObject
public class SecurityBudgetTests
{
    private SecurityBudget budget;

    [TearDown]
    public void TearDown()
    {
        if (budget != null) Object.DestroyImmediate(budget);
    }

    // Each row is (fromCompletedRuns, guards, adaptivePois)
    private SecurityBudget Budget(params (int from, int guards, int pois)[] rows)
    {
        budget = ScriptableObject.CreateInstance<SecurityBudget>();
        SerializedObject so = new SerializedObject(budget);
        SerializedProperty list = so.FindProperty("tiers");
        list.arraySize = rows.Length;

        for (int i = 0; i < rows.Length; i++)
        {
            SerializedProperty tier = list.GetArrayElementAtIndex(i);
            tier.FindPropertyRelative("fromCompletedRuns").intValue = rows[i].from;
            tier.FindPropertyRelative("guards").intValue = rows[i].guards;
            tier.FindPropertyRelative("adaptivePois").intValue = rows[i].pois;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return budget;
    }

    [TestCase(0, 1, 0)]
    [TestCase(1, 1, 1)]
    [TestCase(2, 2, 2)]
    [TestCase(3, 2, 3)]
    [TestCase(9, 2, 3)]
    public void TierFor_FollowsCompletedRuns(int runs, int guards, int pois)
    {
        SecurityBudget b = Budget((0, 1, 0), (1, 1, 1), (2, 2, 2), (3, 2, 3));

        SecurityTier tier = b.TierFor(runs);

        Assert.AreEqual(guards, tier.guards);
        Assert.AreEqual(pois, tier.adaptivePois);
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    public void IsRaised_OnlyOnceGuardsExceedTheFirstRun(int runs, bool raised)
    {
        SecurityBudget b = Budget((0, 1, 0), (1, 1, 1), (2, 2, 2), (3, 2, 3));

        Assert.AreEqual(raised, b.IsRaised(runs));
    }

    [Test]
    public void IsRaised_ComparesWithTheFirstTier_NotAFixedCount()
    {
        // A level that starts with three guards is not raised until it hires a fourth
        SecurityBudget b = Budget((0, 3, 0), (1, 3, 1), (2, 4, 2));

        Assert.IsFalse(b.IsRaised(1));
        Assert.IsTrue(b.IsRaised(2));
    }

    [Test]
    public void IsRaised_EmptyBudget_IsNeverRaised()
    {
        SecurityBudget b = Budget();

        Assert.IsFalse(b.IsRaised(5));
    }
}
