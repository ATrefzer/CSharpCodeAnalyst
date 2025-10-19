using CodeParser.Analysis.Cycles;
using CodeParser.Analysis.Shared;

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

        Assert.AreEqual(1, sccs.Count);
        Assert.AreEqual(3, sccs[0].Vertices.Count);
        Assert.IsTrue(sccs[0].Vertices.Contains(nodeA));
        Assert.IsTrue(sccs[0].Vertices.Contains(nodeB));
        Assert.IsTrue(sccs[0].Vertices.Contains(nodeC));
    }
}