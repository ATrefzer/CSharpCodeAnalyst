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
}
