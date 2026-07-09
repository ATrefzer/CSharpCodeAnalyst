using CSharpCodeAnalyst.TreeMap.Data;

namespace CodeParserTests.UnitTests.TreeMap;

[TestFixture]
public class HierarchicalDataTests
{
    /// <summary>
    ///     root
    ///     ├── a
    ///     │   ├── a1
    ///     │   └── a2
    ///     └── b
    /// </summary>
    private static HierarchicalData BuildTree()
    {
        var root = new HierarchicalData("root");
        var a = new HierarchicalData("a");
        var b = new HierarchicalData("b", 1);
        var a1 = new HierarchicalData("a1", 1);
        var a2 = new HierarchicalData("a2", 1);
        root.AddChild(a);
        root.AddChild(b);
        a.AddChild(a1);
        a.AddChild(a2);
        return root;
    }

    [Test]
    public void TraverseTopDown_VisitsParentsBeforeTheirChildren()
    {
        var visited = new List<string>();

        BuildTree().TraverseTopDown(node => visited.Add(node.Name));

        Assert.That(visited, Is.EqualTo(new[] { "root", "a", "a1", "a2", "b" }));
    }

    [Test]
    public void TraverseBottomUp_VisitsChildrenBeforeTheirParents()
    {
        var visited = new List<string>();

        BuildTree().TraverseBottomUp(node => visited.Add(node.Name));

        Assert.That(visited, Is.EqualTo(new[] { "a1", "a2", "a", "b", "root" }));
    }

    // -------------------------------------------------------------------
    // NormalizeWeightMetrics (rank-based / percentile)
    // -------------------------------------------------------------------

    [Test]
    public void NormalizeWeightMetrics_SkewedWeights_SpreadsRanksEvenly()
    {
        // With min-max normalization the outlier (1000) would push the other three
        // leaves into the bottom 0.2% of the scale; percentile mapping spreads them.
        var root = new HierarchicalData("root");
        var folder = new HierarchicalData("src");
        root.AddChild(folder);

        var a = new HierarchicalData("a.cs", 10, 1);
        var b = new HierarchicalData("b.cs", 10, 2);
        var c = new HierarchicalData("c.cs", 10, 3);
        var d = new HierarchicalData("d.cs", 10, 1000);
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
        var root = new HierarchicalData("root");
        var a = new HierarchicalData("a.cs", 10, 5);
        var b = new HierarchicalData("b.cs", 10, 5);
        var c = new HierarchicalData("c.cs", 10, 5);
        var d = new HierarchicalData("d.cs", 10, 10);
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
        var root = new HierarchicalData("root");
        var a = new HierarchicalData("a.cs", 10, 7);
        var b = new HierarchicalData("b.cs", 10, 7);
        root.AddChild(a);
        root.AddChild(b);

        root.NormalizeWeightMetrics();

        Assert.That(a.NormalizedWeightMetric, Is.EqualTo(0.5));
        Assert.That(b.NormalizedWeightMetric, Is.EqualTo(0.5));
    }

    [Test]
    public void NormalizeWeightMetrics_SingleLeaf_MapsToMidpoint()
    {
        // Degenerate case of "all weights equal".
        var root = new HierarchicalData("root");
        var a = new HierarchicalData("a.cs", 10, 42);
        root.AddChild(a);

        root.NormalizeWeightMetrics();

        Assert.That(a.NormalizedWeightMetric, Is.EqualTo(0.5));
    }
}
