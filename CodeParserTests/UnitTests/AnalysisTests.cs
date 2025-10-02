using CodeParser.Analysis.Cycles;
using Contracts.Graph;

namespace CodeParserTests.UnitTests;

[TestFixture]
public class AnalysisTests
{
    [Test]
    public void FindStronglyConnectedComponents_ShouldFindSCC()
    {
        // Arrange
        var codeStructure = CreateTestCodeStructure();

        // Act
        var sccs = CycleFinder.FindCycleGroups(codeStructure);

        // Assert
        Assert.AreEqual(1, sccs.Count); // We expect one SCC
        var scc = sccs[0];
        Assert.AreEqual(3, scc.CodeGraph.Nodes.Values.Count);
        Assert.True(scc.CodeGraph.Nodes.ContainsKey("A"));
        Assert.True(scc.CodeGraph.Nodes.ContainsKey("B"));
        Assert.True(scc.CodeGraph.Nodes.ContainsKey("C"));
    }

    private static CodeGraph CreateTestCodeStructure()
    {
        var codeStructure = new CodeGraph();

        // Create nodes
        var nodeA = new CodeElement("A", CodeElementType.Class,
            "ClassA",
            "", null);
        var nodeB = new CodeElement("B", CodeElementType.Class,
            "ClassB",
            "", null);
        var nodeC = new CodeElement("C", CodeElementType.Class,
            "ClassC",
            "", null);
        var nodeD = new CodeElement("D", CodeElementType.Class,
            "ClassD",
            "", null);

        // Create dependencies to form a cycle: A -> B -> C -> A
        nodeA.Relationships.Add(new Relationship("A",
            "B", RelationshipType.Calls));
        nodeB.Relationships.Add(new Relationship("B",
            "C", RelationshipType.Calls));
        nodeC.Relationships.Add(new Relationship("C",
            "A", RelationshipType.Calls));

        // Additional dependency: D -> A (to ensure D is not part of the SCC)
        nodeD.Relationships.Add(new Relationship("D",
            "A", RelationshipType.Calls));

        // Add nodes to the code graph
        codeStructure.Nodes["A"] = nodeA;
        codeStructure.Nodes["B"] = nodeB;
        codeStructure.Nodes["C"] = nodeC;
        codeStructure.Nodes["D"] = nodeD;

        return codeStructure;
    }
}