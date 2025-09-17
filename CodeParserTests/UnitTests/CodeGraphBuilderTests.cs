using CodeParser.Analysis.Cycles;
using Contracts.Graph;

namespace CodeParserTests.UnitTests;

/// <summary>
///     Assumes the SearchGraphBuilder test are ok.
/// </summary>
[TestFixture]
public class CodeGraphGeneratorTests
{
    [Test]
    public void GenerateDetailedCodeGraph_SimpleClassDependency_PreservesDependency()
    {
        var originalGraph = new CodeGraph();
        var classA = new CodeElement("A", CodeElementType.Class,
            "ClassA",
            "", null);
        var classB = new CodeElement("B", CodeElementType.Class,
            "ClassB",
            "", null);
        classA.Relationships.Add(new Relationship("A",
            "B", RelationshipType.Uses));
        originalGraph.Nodes["A"] = classA;
        originalGraph.Nodes["B"] = classB;

        var searchGraph = SearchGraphBuilder.BuildSearchGraph(originalGraph);
        var detailedGraph = CodeGraphBuilder.GenerateDetailedCodeGraph(searchGraph.Vertices, originalGraph);

        Assert.AreEqual(2, detailedGraph.Nodes.Count);
        Assert.IsTrue(detailedGraph.Nodes.ContainsKey("A"));
        Assert.IsTrue(detailedGraph.Nodes.ContainsKey("B"));
        Assert.AreEqual(1, detailedGraph.Nodes["A"].Relationships.Count);
        Assert.AreEqual(0, detailedGraph.Nodes["B"].Relationships.Count);
        Assert.AreEqual("B", detailedGraph.Nodes["A"].Relationships.First().TargetId);
    }

    [Test]
    public void GenerateDetailedCodeGraph_MethodDependency_PreservesMethodLevelDependency()
    {
        var originalGraph = new CodeGraph();
        var classA = new CodeElement("A", CodeElementType.Class,
            "ClassA",
            "", null);
        var methodA = new CodeElement("A.M", CodeElementType.Method,
            "MethodA",
            "", classA);
        var classB = new CodeElement("B", CodeElementType.Class,
            "ClassB",
            "", null);
        var methodB = new CodeElement("B.M", CodeElementType.Method,
            "MethodB",
            "", classB);
        methodA.Relationships.Add(new Relationship("A.M",
            "B.M", RelationshipType.Calls));
        classA.Children.Add(methodA);
        classB.Children.Add(methodB);
        originalGraph.Nodes["A"] = classA;
        originalGraph.Nodes["A.M"] = methodA;
        originalGraph.Nodes["B"] = classB;
        originalGraph.Nodes["B.M"] = methodB;

        var searchGraph = SearchGraphBuilder.BuildSearchGraph(originalGraph);
        var detailedGraph = CodeGraphBuilder.GenerateDetailedCodeGraph(searchGraph.Vertices, originalGraph);

        Assert.AreEqual(4, detailedGraph.Nodes.Count);
        Assert.IsTrue(detailedGraph.Nodes.ContainsKey("A"));
        Assert.IsTrue(detailedGraph.Nodes.ContainsKey("A.M"));
        Assert.IsTrue(detailedGraph.Nodes.ContainsKey("B"));
        Assert.IsTrue(detailedGraph.Nodes.ContainsKey("B.M"));
        Assert.AreEqual(1, detailedGraph.Nodes["A.M"].Relationships.Count);
        Assert.AreEqual("B.M", detailedGraph.Nodes["A.M"].Relationships.First().TargetId);
    }

    [Test]
    public void GenerateDetailedCodeGraph_MultipleDependencies_PreservesAllDependencies()
    {
        var originalGraph = new CodeGraph();
        var classA = new CodeElement("A", CodeElementType.Class,
            "ClassA",
            "", null);
        var methodA1 = new CodeElement("A.M1", CodeElementType.Method,
            "MethodA1",
            "", classA);
        var methodA2 = new CodeElement("A.M2", CodeElementType.Method,
            "MethodA2",
            "", classA);
        var classB = new CodeElement("B", CodeElementType.Class,
            "ClassB",
            "", null);
        var methodB = new CodeElement("B.M", CodeElementType.Method,
            "MethodB",
            "", classB);
        methodA1.Relationships.Add(new Relationship("A.M1",
            "B", RelationshipType.Calls));
        methodA2.Relationships.Add(new Relationship("A.M2",
            "B.M", RelationshipType.Calls));
        classA.Children.Add(methodA1);
        classA.Children.Add(methodA2);
        classB.Children.Add(methodB);
        originalGraph.Nodes["A"] = classA;
        originalGraph.Nodes["A.M1"] = methodA1;
        originalGraph.Nodes["A.M2"] = methodA2;
        originalGraph.Nodes["B"] = classB;
        originalGraph.Nodes["B.M"] = methodB;

        var searchGraph = SearchGraphBuilder.BuildSearchGraph(originalGraph);
        var detailedGraph = CodeGraphBuilder.GenerateDetailedCodeGraph(searchGraph.Vertices, originalGraph);

        Assert.AreEqual(5, detailedGraph.Nodes.Count);
        Assert.AreEqual(1, detailedGraph.Nodes["A.M1"].Relationships.Count);
        Assert.AreEqual("B", detailedGraph.Nodes["A.M1"].Relationships.First().TargetId);
        Assert.AreEqual(1, detailedGraph.Nodes["A.M2"].Relationships.Count);
        Assert.AreEqual("B.M", detailedGraph.Nodes["A.M2"].Relationships.First().TargetId);
    }
}