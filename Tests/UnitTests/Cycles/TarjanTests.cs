using CodeGraph.Algorithms.Cycles;

namespace CodeParserTests.UnitTests.Cycles;

[TestFixture]
public class TarjanTests
{
    [Test]
    public void FindStronglyConnectedComponents_SimpleCircularDependency_ReturnsSingleSCC()
    {
        var nodeA = new SearchNode("A", null!);
        var nodeB = new SearchNode("B", null!);
        var nodeC = new SearchNode("C", null!);

        nodeA.Dependencies.Add(nodeB);
        nodeB.Dependencies.Add(nodeC);
        nodeC.Dependencies.Add(nodeA);

        var graph = new List<SearchNode> { nodeA, nodeB, nodeC };

        var sccs = Tarjan.FindStronglyConnectedComponents(new SearchGraph(graph));

        Assert.That(sccs.Count, Is.EqualTo(1));
        Assert.That(sccs[0].Vertices.Count, Is.EqualTo(3));
        Assert.That(sccs[0].Vertices.Contains(nodeA));
        Assert.That(sccs[0].Vertices.Contains(nodeB));
        Assert.That(sccs[0].Vertices.Contains(nodeC));
    }
}