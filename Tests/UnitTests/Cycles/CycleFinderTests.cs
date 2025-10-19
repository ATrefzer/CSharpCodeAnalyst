using CodeParser.Analysis.Cycles;
using CodeParserTests.Helper;
using Contracts.Graph;

// ReSharper disable InconsistentNaming

namespace CodeParserTests.UnitTests.Cycles;

[TestFixture]
public class CycleFinderTests
{
    [Test]
    public void FindClassCycle()
    {
        var codeGraph = new TestCodeGraph();
        var classA = codeGraph.CreateClass("ClassA");
        var classB = codeGraph.CreateClass("ClassB");
        classA.Relationships.Add(new Relationship(classA.Id, classB.Id, RelationshipType.Uses));
        classB.Relationships.Add(new Relationship(classB.Id, classA.Id, RelationshipType.Uses));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(1, groups.Count);
    }


    [Test]
    public void PinSignalViewRegression()
    {
        var codeGraph = new TestCodeGraph();
        var view = codeGraph.CreateClass("View");
        var peer = codeGraph.CreateClass("AutomationPeer");

        var createPeer = codeGraph.CreateMethod("View.OnCreatePeer", view);
        var owner = codeGraph.CreateField("AutomationPeer._owner", peer);
        var ctor = codeGraph.CreateMethod("AutomationPeer.ctor", peer);

        createPeer.Relationships.Add(new Relationship(createPeer.Id, peer.Id, RelationshipType.Creates));
        ctor.Relationships.Add(new Relationship(ctor.Id, view.Id, RelationshipType.Uses));
        owner.Relationships.Add(new Relationship(owner.Id, view.Id, RelationshipType.Uses));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        //var export = new DgmlExport();
        //export.Export("d:\\out.dgml", codeGraph);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(5, groups.First().CodeGraph.Nodes.Count);
    }

    [Test]
    public void Regression_NestedClasses()
    {
        // Actually after thinking about this constellation I think it is not a cycle.
        // Enum is a type like class. They should be treated equally.

        var codeGraph = new TestCodeGraph();

        // 10 elements, 7 used in SCC.
        var classParent = codeGraph.CreateClass("Class_Parent");
        var methodInParent = codeGraph.CreateMethod("Method", classParent);

        var classChild1 = codeGraph.CreateClass("Class_Child1", classParent);
        var classChild2 = codeGraph.CreateClass("Class_Child2", classParent);

        // Directly in parent
        var enumInParent = codeGraph.CreateEnum("EnumInParent", classParent);


        var field1 = codeGraph.CreateField("_field1", classChild1);
        var field2 = codeGraph.CreateField("_field2", classChild2);


        field1.Relationships.Add(new Relationship(field1.Id, enumInParent.Id, RelationshipType.Uses));
        field2.Relationships.Add(new Relationship(field2.Id, enumInParent.Id, RelationshipType.Uses));
        methodInParent.Relationships.Add(new Relationship(methodInParent.Id, classChild1.Id, RelationshipType.Uses));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(0, groups.Count);

        //var export = new DgmlExport();
        //export.Export("d:\\out.dgml", codeGraph);
    }

    [Test]
    public void Regression_NestedNamespaces()
    {
        var codeGraph = new TestCodeGraph();

        // 10 elements, 7 used in SCC.
        var nsParent = codeGraph.CreateNamespace("NS_Parent");

        var nsChild = codeGraph.CreateNamespace("NS_Child", nsParent);

        // Directly in parent
        var classNsParent = codeGraph.CreateClass("ClassInParent", nsParent);
        var field1 = codeGraph.CreateField("_delegate1", classNsParent);

        var classNsChild = codeGraph.CreateClass("ClassInChild", nsChild);
        var delegateNsChild = codeGraph.CreateClass("DelegateInChild", nsChild);
        var method = codeGraph.CreateMethod("Method", classNsChild);

        // Just a reference from a cycle irrelevant namespace to the delegate in NS_Child
        var nsIrrelevant = codeGraph.CreateNamespace("NS_Irrelevant", nsParent);
        var classNsIrrelevant = codeGraph.CreateClass("ClassNsIrrelevant", nsIrrelevant);
        var field2 = codeGraph.CreateField("_delegate2", classNsIrrelevant);

        field1.Relationships.Add(new Relationship(field1.Id, delegateNsChild.Id, RelationshipType.Uses));
        field2.Relationships.Add(new Relationship(field2.Id, delegateNsChild.Id, RelationshipType.Uses));
        method.Relationships.Add(new Relationship(method.Id, classNsParent.Id, RelationshipType.Uses));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(7, groups.First().CodeGraph.Nodes.Count);

        //var export = new DgmlExport();
        //export.Export("d:\\nested_namespaces.dgml", codeGraph);
    }

    [Test]
    public void FindClassCycleViaField()
    {
        var codeGraph = new TestCodeGraph();
        var classA = codeGraph.CreateClass("ClassA");
        var classB = codeGraph.CreateClass("ClassB");

        var fieldA = codeGraph.CreateField("ClassA.FieldA", classA);
        var fieldB = codeGraph.CreateField("ClassA.FieldB", classB);
        classA.Relationships.Add(new Relationship(fieldA.Id, classB.Id, RelationshipType.Uses));
        classB.Relationships.Add(new Relationship(fieldB.Id, classA.Id, RelationshipType.Uses));


        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(4, groups.First().CodeGraph.Nodes.Count);
    }

    [Test]
    public void FindMethodCrossNamespaceCycle()
    {
        var codeGraph = new TestCodeGraph();

        var ns1 = codeGraph.CreateNamespace("NS1");
        var ns2 = codeGraph.CreateNamespace("NS2");
        var classA = codeGraph.CreateClass("ClassA", ns1);
        var classB = codeGraph.CreateClass("ClassB", ns2);
        var methodAA = codeGraph.CreateMethod("ClassA.MethodA", classA);
        var methodAB = codeGraph.CreateMethod("ClassA.MethodB", classA);
        var methodBA = codeGraph.CreateMethod("ClassB.MethodA", classB);
        var methodBB = codeGraph.CreateMethod("ClassB.MethodB", classB);

        methodAA.Relationships.Add(new Relationship(methodAA.Id, methodBA.Id, RelationshipType.Calls));
        methodBB.Relationships.Add(new Relationship(methodBB.Id, methodAB.Id, RelationshipType.Calls));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(8, groups.First().CodeGraph.Nodes.Count);
    }

    [Test]
    public void FindMethodCycle()
    {
        var codeGraph = new TestCodeGraph();

        var classA = codeGraph.CreateClass("ClassA");
        var classB = codeGraph.CreateClass("ClassB");
        var methodA = codeGraph.CreateMethod("ClassA.MethodA", classA);
        var methodB = codeGraph.CreateMethod("ClassB.MethodB", classB);
        methodA.Relationships.Add(new Relationship(methodA.Id, methodB.Id, RelationshipType.Calls));
        methodB.Relationships.Add(new Relationship(methodB.Id, methodA.Id, RelationshipType.Calls));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(4, groups.First().CodeGraph.Nodes.Count);
    }

    [Test]
    public void FindCycleInNestedNamespaces()
    {
        // Tests cycle detection between classes in nested namespaces.

        var codeGraph = new TestCodeGraph();
        var ns1 = codeGraph.CreateNamespace("NS1");
        var ns2 = codeGraph.CreateNamespace("NS1.NS2", ns1);
        var classA = codeGraph.CreateClass("ClassA", ns1);
        var classB = codeGraph.CreateClass("ClassB", ns2);
        classA.Relationships.Add(new Relationship(classA.Id, classB.Id, RelationshipType.Uses));
        classB.Relationships.Add(new Relationship(classB.Id, classA.Id, RelationshipType.Uses));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(4, groups.First().CodeGraph.Nodes.Count);
    }

    [Test]
    public void FindCycleBetweenNestedClasses()
    {
        // Checks for cycles between nested classes within an outer class.

        var codeGraph = new TestCodeGraph();
        var outerClass = codeGraph.CreateClass("OuterClass");
        var innerClassA = codeGraph.CreateClass("InnerClassA", outerClass);
        var innerClassB = codeGraph.CreateClass("InnerClassB", outerClass);
        innerClassA.Relationships.Add(new Relationship(innerClassA.Id, innerClassB.Id, RelationshipType.Uses));
        innerClassB.Relationships.Add(new Relationship(innerClassB.Id, innerClassA.Id, RelationshipType.Uses));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(2, groups.First().CodeGraph.Nodes.Count);
    }

    [Test]
    public void FindCycleBetweenMethodAndNestedClass()
    {
        // Tests cycle detection between a method and a nested class.

        var codeGraph = new TestCodeGraph();
        var outerClass = codeGraph.CreateClass("OuterClass");
        var innerClass = codeGraph.CreateClass("InnerClass", outerClass);
        var method = codeGraph.CreateMethod("OuterClass.Method", outerClass);
        method.Relationships.Add(new Relationship(method.Id, innerClass.Id, RelationshipType.Uses));
        innerClass.Relationships.Add(new Relationship(innerClass.Id, method.Id, RelationshipType.Calls));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(3, groups.First().CodeGraph.Nodes.Count);
    }

    [Test]
    public void FindCycleBetweenNamespaceAndClass()
    {
        // Verifies cycle detection between a namespace and a class in another namespace.
        // Not a realistic scenario, but useful for testing.

        var codeGraph = new TestCodeGraph();
        var ns1 = codeGraph.CreateNamespace("NS1");
        var ns2 = codeGraph.CreateNamespace("NS2");
        var classA = codeGraph.CreateClass("ClassA", ns1);
        var classB = codeGraph.CreateClass("ClassB", ns2);
        ns1.Relationships.Add(new Relationship(ns1.Id, classB.Id, RelationshipType.Uses));
        classB.Relationships.Add(new Relationship(classB.Id, ns1.Id, RelationshipType.Uses));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(1, groups.Count);

        // Note: ClassA is missing because there is no dependency in the original graph
        // to or from ClassA
        Assert.AreEqual(3, groups.First().CodeGraph.Nodes.Count);
    }

    [Test]
    public void NoCycleBetweenContainedElements()
    {
        // Ensures that containment relationships don't create cycles.
        var codeGraph = new TestCodeGraph();
        var ns = codeGraph.CreateNamespace("NS");
        var classA = codeGraph.CreateClass("ClassA", ns);
        var methodA = codeGraph.CreateMethod("ClassA.MethodA", classA);
        ns.Relationships.Add(new Relationship(ns.Id, classA.Id, RelationshipType.Containment));
        classA.Relationships.Add(new Relationship(classA.Id, methodA.Id, RelationshipType.Containment));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(0, groups.Count);
    }

    [Test]
    public void FindCycleBetweenInterfaceAndImplementingClass()
    {
        // Tests cycle detection between an interface and its implementing class.

        var codeGraph = new TestCodeGraph();
        var interfaceA = codeGraph.CreateInterface("InterfaceA");
        var classA = codeGraph.CreateClass("ClassA");
        classA.Relationships.Add(new Relationship(classA.Id, interfaceA.Id, RelationshipType.Implements));
        interfaceA.Relationships.Add(new Relationship(interfaceA.Id, classA.Id, RelationshipType.Uses));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(2, groups.First().CodeGraph.Nodes.Count);
    }

    [Test]
    public void FindCycleBetweenEnumAndClass()
    {
        // Checks for cycles between an enum and a class.

        var codeGraph = new TestCodeGraph();
        var enumA = codeGraph.CreateEnum("EnumA");
        var classA = codeGraph.CreateClass("ClassA");
        classA.Relationships.Add(new Relationship(classA.Id, enumA.Id, RelationshipType.Uses));
        enumA.Relationships.Add(new Relationship(enumA.Id, classA.Id, RelationshipType.Uses));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(2, groups.First().CodeGraph.Nodes.Count);
    }

    [Test]
    public void FindMultipleCyclesInGraph()
    {
        // Verifies that multiple distinct cycles in the graph are detected correctly.

        var codeGraph = new TestCodeGraph();
        var classA = codeGraph.CreateClass("ClassA");
        var classB = codeGraph.CreateClass("ClassB");
        var classC = codeGraph.CreateClass("ClassC");
        var classD = codeGraph.CreateClass("ClassD");

        classA.Relationships.Add(new Relationship(classA.Id, classB.Id, RelationshipType.Uses));
        classB.Relationships.Add(new Relationship(classB.Id, classA.Id, RelationshipType.Uses));

        classC.Relationships.Add(new Relationship(classC.Id, classD.Id, RelationshipType.Uses));
        classD.Relationships.Add(new Relationship(classD.Id, classC.Id, RelationshipType.Uses));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(2, groups.Count);
        Assert.AreEqual(2, groups[0].CodeGraph.Nodes.Count);
        Assert.AreEqual(2, groups[1].CodeGraph.Nodes.Count);
    }

    /// <summary>
    ///     This is an older test moved to this file.
    /// </summary>
    [Test]
    public void FindStronglyConnectedComponents_ShouldFindSCC()
    {
        // Arrange
        var codeStructure = CreateCodeGraphForShouldFindScc();

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

    private static CodeGraph CreateCodeGraphForShouldFindScc()
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