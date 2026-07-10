using CSharpCodeAnalyst.Contracts;
using CSharpCodeAnalyst.History.Hierarchy;

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

    // -------------------------------------------------------------------
    // GetPathToRoot / Clone - the mechanism the zoom restoration relies on
    // after the Id field was removed (see HierarchicalDataViewBase.FindByPath).
    // -------------------------------------------------------------------

    [Test]
    public void GetPathToRoot_BuildsSlashSeparatedPathFromRoot()
    {
        var root = BuildTree();
        var a1 = Collect(root).Single(n => n.Name == "a1");

        Assert.That(root.GetPathToRoot(), Is.EqualTo("/root"));
        Assert.That(a1.GetPathToRoot(), Is.EqualTo("/root/a/a1"));
    }

    [Test]
    public void GetPathToRoot_IsUniqueForEveryNode()
    {
        // FindByPath identifies a node by its path, so paths must be unique.
        var nodes = Collect(BuildTree());

        var paths = nodes.Select(n => n.GetPathToRoot()).ToList();

        Assert.That(paths.Distinct().Count(), Is.EqualTo(nodes.Count));
    }

    [Test]
    public void Clone_ProducesIndependentInstance()
    {
        var original = BuildTree();

        var clone = original.Clone();
        clone.Description = "changed";

        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(original.Description, Is.EqualTo("root"));
    }

    [Test]
    public void Clone_PreservesPathToRootForEveryNode()
    {
        // The zoom level captured before filtering is re-located in the filtered clone by path,
        // so cloning must reproduce every node's path exactly.
        var original = BuildTree();

        var clone = original.Clone();

        var originalPaths = Collect(original).Select(n => n.GetPathToRoot()).OrderBy(p => p);
        var clonePaths = Collect(clone).Select(n => n.GetPathToRoot()).OrderBy(p => p);
        Assert.That(clonePaths, Is.EqualTo(originalPaths));
    }

    [Test]
    public void Clone_CopiesLeafDataFields()
    {
        var root = new HierarchicalData("root");
        var leaf = new HierarchicalData("f.cs", 10, 3) { Description = "desc", ColorKey = "dev", Tag = "tag" };
        root.AddChild(leaf);
        root.SumAreaMetrics();
        root.NormalizeWeightMetrics();

        var cloneLeaf = root.Clone().Children.Single();

        Assert.That(cloneLeaf.Name, Is.EqualTo("f.cs"));
        Assert.That(cloneLeaf.AreaMetric, Is.EqualTo(10));
        Assert.That(cloneLeaf.WeightMetric, Is.EqualTo(3));
        Assert.That(cloneLeaf.Description, Is.EqualTo("desc"));
        Assert.That(cloneLeaf.ColorKey, Is.EqualTo("dev"));
        Assert.That(cloneLeaf.Tag, Is.EqualTo("tag"));
        Assert.That(cloneLeaf.NormalizedWeightMetric, Is.EqualTo(leaf.NormalizedWeightMetric));
    }

    private static List<IHierarchicalData> Collect(IHierarchicalData root)
    {
        var nodes = new List<IHierarchicalData>();
        root.TraverseTopDown(nodes.Add);
        return nodes;
    }
}
