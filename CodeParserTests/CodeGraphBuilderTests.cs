using CodeParser.Analysis.Cycles;
using Contracts.Graph;

namespace CodeParserTests;

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
        var classA = new CodeElement("A", CodeElementType.Class, "ClassA", "", null);
        var classB = new CodeElement("B", CodeElementType.Class, "ClassB", "", null);
        classA.Dependencies.Add(new Dependency("A", "B", DependencyType.Uses));
        originalGraph.Nodes["A"] = classA;
        originalGraph.Nodes["B"] = classB;

        var searchGraph = SearchGraphBuilder.BuildSearchGraph(originalGraph);
        var detailedGraph = CodeGraphBuilder.GenerateDetailedCodeGraph(searchGraph.Vertices, originalGraph);

        Assert.AreEqual(2, detailedGraph.Nodes.Count);
        Assert.IsTrue(detailedGraph.Nodes.ContainsKey("A"));
        Assert.IsTrue(detailedGraph.Nodes.ContainsKey("B"));
        Assert.AreEqual(1, detailedGraph.Nodes["A"].Dependencies.Count);
        Assert.AreEqual(0, detailedGraph.Nodes["B"].Dependencies.Count);
        Assert.AreEqual("B", detailedGraph.Nodes["A"].Dependencies.First().TargetId);
    }

    [Test]
    public void GenerateDetailedCodeGraph_MethodDependency_PreservesMethodLevelDependency()
    {
        var originalGraph = new CodeGraph();
        var classA = new CodeElement("A", CodeElementType.Class, "ClassA", "", null);
        var methodA = new CodeElement("A.M", CodeElementType.Method, "MethodA", "", classA);
        var classB = new CodeElement("B", CodeElementType.Class, "ClassB", "", null);
        var methodB = new CodeElement("B.M", CodeElementType.Method, "MethodB", "", classB);
        methodA.Dependencies.Add(new Dependency("A.M", "B.M", DependencyType.Calls));
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
        Assert.AreEqual(1, detailedGraph.Nodes["A.M"].Dependencies.Count);
        Assert.AreEqual("B.M", detailedGraph.Nodes["A.M"].Dependencies.First().TargetId);
    }

    [Test]
    public void GenerateDetailedCodeGraph_MultipleDependencies_PreservesAllDependencies()
    {
        var originalGraph = new CodeGraph();
        var classA = new CodeElement("A", CodeElementType.Class, "ClassA", "", null);
        var methodA1 = new CodeElement("A.M1", CodeElementType.Method, "MethodA1", "", classA);
        var methodA2 = new CodeElement("A.M2", CodeElementType.Method, "MethodA2", "", classA);
        var classB = new CodeElement("B", CodeElementType.Class, "ClassB", "", null);
        var methodB = new CodeElement("B.M", CodeElementType.Method, "MethodB", "", classB);
        methodA1.Dependencies.Add(new Dependency("A.M1", "B", DependencyType.Calls));
        methodA2.Dependencies.Add(new Dependency("A.M2", "B.M", DependencyType.Calls));
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
        Assert.AreEqual(1, detailedGraph.Nodes["A.M1"].Dependencies.Count);
        Assert.AreEqual("B", detailedGraph.Nodes["A.M1"].Dependencies.First().TargetId);
        Assert.AreEqual(1, detailedGraph.Nodes["A.M2"].Dependencies.Count);
        Assert.AreEqual("B.M", detailedGraph.Nodes["A.M2"].Dependencies.First().TargetId);
    }
}