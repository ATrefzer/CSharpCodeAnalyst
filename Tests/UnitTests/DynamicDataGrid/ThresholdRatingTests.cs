using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;

namespace CodeParserTests.UnitTests.DynamicDataGrid;

[TestFixture]
public class ThresholdRatingTests
{
    [TestCase(1, RatingLevel.Good)]
    [TestCase(10, RatingLevel.Good)] // boundary: goodMax is inclusive
    [TestCase(11, RatingLevel.Warning)]
    [TestCase(20, RatingLevel.Warning)] // boundary: warningMax is inclusive
    [TestCase(21, RatingLevel.Bad)]
    [TestCase(100, RatingLevel.Bad)]
    public void HigherIsWorse_RatesByUpperBounds(double value, RatingLevel expected)
    {
        // Cyclomatic complexity style: larger = worse.
        var rating = new ThresholdRating(10, 20);

        Assert.That(rating.Evaluate(value), Is.EqualTo(expected));
    }

    [TestCase(90, RatingLevel.Good)]
    [TestCase(80, RatingLevel.Good)] // boundary: goodMax is inclusive
    [TestCase(79, RatingLevel.Warning)]
    [TestCase(50, RatingLevel.Warning)] // boundary: warningMax is inclusive
    [TestCase(49, RatingLevel.Bad)]
    [TestCase(0, RatingLevel.Bad)]
    public void LowerIsWorse_RatesByLowerBounds(double value, RatingLevel expected)
    {
        // e.g. a percentage where smaller = worse.
        var rating = new ThresholdRating(80, 50, higherIsWorse: false);

        Assert.That(rating.Evaluate(value), Is.EqualTo(expected));
    }
}
