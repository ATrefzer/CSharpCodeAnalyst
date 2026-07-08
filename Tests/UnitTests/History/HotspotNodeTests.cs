using CSharpCodeAnalyst.History.Analyzer;

namespace CodeParserTests.UnitTests.History;

[TestFixture]
public class HotspotNodeTests
{
    [Test]
    public void NormalizeWeightMetrics_SkewedWeights_SpreadsRanksEvenly()
    {
        // With min-max normalization the outlier (1000) would push the other three
        // leaves into the bottom 0.2% of the scale; percentile mapping spreads them.
        var root = new HotspotNode("");
        var folder = new HotspotNode("src");
        root.AddChild(folder);

        var a = new HotspotNode("a.cs", 10, 1);
        var b = new HotspotNode("b.cs", 10, 2);
        var c = new HotspotNode("c.cs", 10, 3);
        var d = new HotspotNode("d.cs", 10, 1000);
        folder.AddChild(a);
        folder.AddChild(b);
        root.AddChild(c);
        root.AddChild(d);

        root.NormalizeWeightMetrics();

        Assert.That(a.NormalizedWeightMetric, Is.EqualTo(0.0));
        Assert.That(b.NormalizedWeightMetric, Is.EqualTo(1.0 / 3).Within(1e-12));
        Assert.That(c.NormalizedWeightMetric, Is.EqualTo(2.0 / 3).Within(1e-12));
        Assert.That(d.NormalizedWeightMetric, Is.EqualTo(1.0));
    }

    [Test]
    public void NormalizeWeightMetrics_EqualWeights_GetTheSamePercentile()
    {
        var root = new HotspotNode("");
        var a = new HotspotNode("a.cs", 10, 5);
        var b = new HotspotNode("b.cs", 10, 5);
        var c = new HotspotNode("c.cs", 10, 5);
        var d = new HotspotNode("d.cs", 10, 10);
        root.AddChild(a);
        root.AddChild(b);
        root.AddChild(c);
        root.AddChild(d);

        root.NormalizeWeightMetrics();

        // The three tied leaves share the percentile of their average rank (0+1+2)/3 = 1.
        Assert.That(a.NormalizedWeightMetric, Is.EqualTo(1.0 / 3).Within(1e-12));
        Assert.That(b.NormalizedWeightMetric, Is.EqualTo(a.NormalizedWeightMetric));
        Assert.That(c.NormalizedWeightMetric, Is.EqualTo(a.NormalizedWeightMetric));
        Assert.That(d.NormalizedWeightMetric, Is.EqualTo(1.0));
    }

    [Test]
    public void NormalizeWeightMetrics_AllWeightsEqual_MapsToMidpointInsteadOfNaN()
    {
        // Min-max would divide by zero here (range 0).
        var root = new HotspotNode("");
        var a = new HotspotNode("a.cs", 10, 7);
        var b = new HotspotNode("b.cs", 10, 7);
        root.AddChild(a);
        root.AddChild(b);

        root.NormalizeWeightMetrics();

        Assert.That(a.NormalizedWeightMetric, Is.EqualTo(0.5));
        Assert.That(b.NormalizedWeightMetric, Is.EqualTo(0.5));
    }

    [Test]
    public void NormalizeWeightMetrics_SingleLeaf_MapsToOne()
    {
        var root = new HotspotNode("");
        var a = new HotspotNode("a.cs", 10, 42);
        root.AddChild(a);

        root.NormalizeWeightMetrics();

        Assert.That(a.NormalizedWeightMetric, Is.EqualTo(1.0));
    }
}
