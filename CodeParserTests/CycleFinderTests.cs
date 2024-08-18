using CodeParser.Analysis.Cycles;
using Contracts.Graph;

namespace CodeParserTests;

[TestFixture]
public partial class CycleFinderTests
{
    [Test]
    public void FindClassCycle()
    {
        var codeGraph = new TestCodeGraph();
        var classA = codeGraph.CreateClass("ClassA");
        var classB = codeGraph.CreateClass("ClassB");
        classA.Dependencies.Add(new Dependency(classA.Id, classB.Id, DependencyType.Uses));
        classB.Dependencies.Add(new Dependency(classB.Id, classA.Id, DependencyType.Uses));

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

        createPeer.Dependencies.Add(new Dependency(createPeer.Id, peer.Id, DependencyType.Creates));
        ctor.Dependencies.Add(new Dependency(ctor.Id, view.Id, DependencyType.Uses));
        owner.Dependencies.Add(new Dependency(owner.Id, view.Id, DependencyType.Uses));

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
        // Enum is a type like class and they should be treated equally.

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


        field1.Dependencies.Add(new Dependency(field1.Id, enumInParent.Id, DependencyType.Uses));
        field2.Dependencies.Add(new Dependency(field2.Id, enumInParent.Id, DependencyType.Uses));
        methodInParent.Dependencies.Add(new Dependency(methodInParent.Id, classChild1.Id, DependencyType.Uses));

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

        field1.Dependencies.Add(new Dependency(field1.Id, delegateNsChild.Id, DependencyType.Uses));
        field2.Dependencies.Add(new Dependency(field2.Id, delegateNsChild.Id, DependencyType.Uses));
        method.Dependencies.Add(new Dependency(method.Id, classNsParent.Id, DependencyType.Uses));

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
        classA.Dependencies.Add(new Dependency(fieldA.Id, classB.Id, DependencyType.Uses));
        classB.Dependencies.Add(new Dependency(fieldB.Id, classA.Id, DependencyType.Uses));


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

        methodAA.Dependencies.Add(new Dependency(methodAA.Id, methodBA.Id, DependencyType.Calls));
        methodBB.Dependencies.Add(new Dependency(methodBB.Id, methodAB.Id, DependencyType.Calls));

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
        methodA.Dependencies.Add(new Dependency(methodA.Id, methodB.Id, DependencyType.Calls));
        methodB.Dependencies.Add(new Dependency(methodB.Id, methodA.Id, DependencyType.Calls));

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
        classA.Dependencies.Add(new Dependency(classA.Id, classB.Id, DependencyType.Uses));
        classB.Dependencies.Add(new Dependency(classB.Id, classA.Id, DependencyType.Uses));

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
        innerClassA.Dependencies.Add(new Dependency(innerClassA.Id, innerClassB.Id, DependencyType.Uses));
        innerClassB.Dependencies.Add(new Dependency(innerClassB.Id, innerClassA.Id, DependencyType.Uses));

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
        method.Dependencies.Add(new Dependency(method.Id, innerClass.Id, DependencyType.Uses));
        innerClass.Dependencies.Add(new Dependency(innerClass.Id, method.Id, DependencyType.Calls));

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
        ns1.Dependencies.Add(new Dependency(ns1.Id, classB.Id, DependencyType.Uses));
        classB.Dependencies.Add(new Dependency(classB.Id, ns1.Id, DependencyType.Uses));

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
        ns.Dependencies.Add(new Dependency(ns.Id, classA.Id, DependencyType.Containment));
        classA.Dependencies.Add(new Dependency(classA.Id, methodA.Id, DependencyType.Containment));

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
        classA.Dependencies.Add(new Dependency(classA.Id, interfaceA.Id, DependencyType.Implements));
        interfaceA.Dependencies.Add(new Dependency(interfaceA.Id, classA.Id, DependencyType.Uses));

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
        classA.Dependencies.Add(new Dependency(classA.Id, enumA.Id, DependencyType.Uses));
        enumA.Dependencies.Add(new Dependency(enumA.Id, classA.Id, DependencyType.Uses));

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

        classA.Dependencies.Add(new Dependency(classA.Id, classB.Id, DependencyType.Uses));
        classB.Dependencies.Add(new Dependency(classB.Id, classA.Id, DependencyType.Uses));

        classC.Dependencies.Add(new Dependency(classC.Id, classD.Id, DependencyType.Uses));
        classD.Dependencies.Add(new Dependency(classD.Id, classC.Id, DependencyType.Uses));

        var groups = CycleFinder.FindCycleGroups(codeGraph);

        Assert.AreEqual(2, groups.Count);
        Assert.AreEqual(2, groups[0].CodeGraph.Nodes.Count);
        Assert.AreEqual(2, groups[1].CodeGraph.Nodes.Count);
    }
}