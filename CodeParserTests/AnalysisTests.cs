using CodeParser.Analysis.Cycles;
using Contracts.Graph;

namespace CodeParserTests;

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

    private CodeGraph CreateTestCodeStructure()
    {
        var codeStructure = new CodeGraph();

        // Create nodes
        var nodeA = new CodeElement("A", CodeElementType.Class, "ClassA", "", null);
        var nodeB = new CodeElement("B", CodeElementType.Class, "ClassB", "", null);
        var nodeC = new CodeElement("C", CodeElementType.Class, "ClassC", "", null);
        var nodeD = new CodeElement("D", CodeElementType.Class, "ClassD", "", null);

        // Create dependencies to form a cycle: A -> B -> C -> A
        nodeA.Dependencies.Add(new Dependency("A", "B", DependencyType.Calls));
        nodeB.Dependencies.Add(new Dependency("B", "C", DependencyType.Calls));
        nodeC.Dependencies.Add(new Dependency("C", "A", DependencyType.Calls));

        // Additional dependency: D -> A (to ensure D is not part of the SCC)
        nodeD.Dependencies.Add(new Dependency("D", "A", DependencyType.Calls));

        // Add nodes to the code graph
        codeStructure.Nodes["A"] = nodeA;
        codeStructure.Nodes["B"] = nodeB;
        codeStructure.Nodes["C"] = nodeC;
        codeStructure.Nodes["D"] = nodeD;

        return codeStructure;
    }
}