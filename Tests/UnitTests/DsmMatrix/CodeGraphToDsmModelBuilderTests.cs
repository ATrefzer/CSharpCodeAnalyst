using System.Reflection;
using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Features.DsmMatrix;
using DsmSuite.DsmViewer.Model.Core;
using DsmSuite.DsmViewer.Model.Interfaces;

namespace CodeParserTests.UnitTests.DsmMatrix;

[TestFixture]
public class CodeGraphToDsmModelBuilderTests
{
    [SetUp]
    public void SetUp()
    {
        _graph = new TestCodeGraph();
        _dsmModel = new DsmModel("Test", Assembly.GetExecutingAssembly());
    }

    private TestCodeGraph _graph = null!;
    private IDsmModel _dsmModel = null!;

    private static void Rel(CodeElement source, CodeElement target, RelationshipType type)
    {
        source.Relationships.Add(new Relationship(source.Id, target.Id, type));
    }

    private int Build()
    {
        return new CodeGraphToDsmModelBuilder(_dsmModel, _graph).Build();
    }

    /// <summary>Elements in the model, without the implicit root that DsmModel always carries.</summary>
    private int AddedElementCount()
    {
        return _dsmModel.GetElementCount() - 1;
    }

    [Test]
    public void Build_EmptyGraph_AddsNothing()
    {
        Assert.That(Build(), Is.EqualTo(0));
        Assert.That(AddedElementCount(), Is.EqualTo(0));
    }

    [Test]
    public void Build_TypeUnderNamespace_RecreatesHierarchyFromParentChain()
    {
        var assembly = _graph.CreateAssembly("Asm");
        var ns = _graph.CreateNamespace("Ns", assembly);
        _graph.CreateClass("A", ns);

        Build();

        // The full name must come from the parent chain, not from splitting a dotted name.
        var element = _dsmModel.GetElementByFullname("Asm.Ns.A");
        Assert.That(element, Is.Not.Null);
        Assert.That(element.Type, Is.EqualTo(CodeElementType.Class.ToString()));
    }

    [Test]
    public void Build_TypeNameContainsDots_StaysASingleElement()
    {
        // This is what the DSI detour cannot do: a name with dots would become a hierarchy there.
        var assembly = _graph.CreateAssembly("Asm");
        _graph.CreateClass("Weird", assembly, "Asm.Weird");
        _graph.Nodes["Weird"].Parent!.Children.Add(_graph.Nodes["Weird"]);

        Build();

        Assert.That(AddedElementCount(), Is.EqualTo(2), "assembly plus one type, no split");
    }

    [Test]
    public void Build_MethodCallsBetweenTypes_LiftedToOneTypeRelation()
    {
        var a = _graph.CreateClass("A");
        var m1 = _graph.CreateMethod("A.M1", a);
        var m2 = _graph.CreateMethod("A.M2", a);
        var b = _graph.CreateClass("B");
        var bm = _graph.CreateMethod("B.M", b);

        Rel(m1, bm, RelationshipType.Calls);
        Rel(m2, bm, RelationshipType.Calls);

        Assert.That(Build(), Is.EqualTo(2));

        var consumer = _dsmModel.GetElementByFullname("A");
        var provider = _dsmModel.GetElementByFullname("B");
        Assert.Multiple(() =>
        {
            Assert.That(_dsmModel.GetRelationCount(consumer, provider), Is.EqualTo(1), "two calls, one edge");
            Assert.That(_dsmModel.GetRelationCount(provider, consumer), Is.EqualTo(0));
        });
    }

    [Test]
    public void Build_MethodsAndFields_AreNotAddedAsElements()
    {
        var a = _graph.CreateClass("A");
        _graph.CreateMethod("A.M", a);
        _graph.CreateField("A.F", a);

        Assert.That(Build(), Is.EqualTo(1));
        Assert.That(AddedElementCount(), Is.EqualTo(1), "only the type itself");
    }

    [Test]
    public void Build_ExternalType_IsExcluded()
    {
        var a = _graph.CreateClass("A");
        var external = _graph.CreateExternalClass("Ext");
        Rel(a, external, RelationshipType.Uses);

        Assert.That(Build(), Is.EqualTo(1));
        Assert.That(_dsmModel.GetElementByFullname("Ext"), Is.Null);
    }

    [Test]
    public void Build_MutualDependency_KeepsBothDirections()
    {
        // The case the matrix paints black, so both directions have to survive.
        var a = _graph.CreateClass("A");
        var b = _graph.CreateClass("B");
        Rel(a, b, RelationshipType.Uses);
        Rel(b, a, RelationshipType.Uses);

        Build();

        var elementA = _dsmModel.GetElementByFullname("A");
        var elementB = _dsmModel.GetElementByFullname("B");
        Assert.Multiple(() =>
        {
            Assert.That(_dsmModel.GetRelationCount(elementA, elementB), Is.EqualTo(1));
            Assert.That(_dsmModel.GetRelationCount(elementB, elementA), Is.EqualTo(1));
        });
    }

    [Test]
    public void Build_AcyclicChain_IsOrderedSoAllDependenciesSitOnOneSide()
    {
        // A -> B -> C, added in an order that is not the dependency order. Partitioning has to fix that,
        // otherwise an acyclic structure does not look acyclic in the matrix.
        var b = _graph.CreateClass("B");
        var c = _graph.CreateClass("C");
        var a = _graph.CreateClass("A");
        Rel(a, b, RelationshipType.Uses);
        Rel(b, c, RelationshipType.Uses);

        Build();

        // The matrix reads cell[row][column] as "column depends on row", so a provider must come after its
        // consumer for the weight to land below the diagonal.
        var order = _dsmModel.RootElement.Children.Select(e => e.Name).ToList();
        Assert.That(order.IndexOf("A"), Is.LessThan(order.IndexOf("B")));
        Assert.That(order.IndexOf("B"), Is.LessThan(order.IndexOf("C")));
    }

    [Test]
    public void Build_SortsWithinAParentOnly_HierarchyIsPreserved()
    {
        var assembly = _graph.CreateAssembly("Asm");
        var ns = _graph.CreateNamespace("Ns", assembly);
        var x = _graph.CreateClass("X", ns);
        var y = _graph.CreateClass("Y", ns);
        Rel(x, y, RelationshipType.Uses);

        Build();

        var nsElement = _dsmModel.GetElementByFullname("Asm.Ns");
        Assert.That(nsElement.Children.Select(e => e.Name), Is.EquivalentTo(new[] { "X", "Y" }),
            "partitioning must reorder siblings, never move them out of their parent");
    }

    [Test]
    public void Build_NamespaceChainWithoutOwnTypes_IsMergedIntoOneLevel()
    {
        // The real shape: the parser makes one element per namespace segment, so an assembly whose root
        // namespace repeats its own name produces a chain of levels that hold nothing.
        var assembly = _graph.CreateAssembly("CSharpCodeAnalyst.CodeGraph");
        var outer = _graph.CreateNamespace("CSharpCodeAnalyst", assembly);
        var inner = _graph.CreateNamespace("CodeGraph", outer);
        var graphNs = _graph.CreateNamespace("Graph", inner);
        var algorithms = _graph.CreateNamespace("Algorithms", inner);
        _graph.CreateClass("CodeElement", graphNs);
        _graph.CreateClass("TypeGraph", algorithms);

        Build();

        // "CSharpCodeAnalyst" has a single namespace child and does not become a level of its own; "CodeGraph"
        // branches and does. The merged label is the namespace that actually exists.
        var assemblyElement = _dsmModel.GetElementByFullname("CSharpCodeAnalyst.CodeGraph");
        Assert.That(assemblyElement.Children.Select(e => e.Name),
            Is.EquivalentTo(new[] { "CSharpCodeAnalyst.CodeGraph" }),
            "the pass-through level must not sit between the assembly and the branching namespace");
    }

    [Test]
    public void Build_ChainOfSeveralPassThroughs_MergesAllOfTheirNames()
    {
        // A -> B -> C where only C branches: one level named after all three.
        var assembly = _graph.CreateAssembly("Asm");
        var a = _graph.CreateNamespace("A", assembly);
        var b = _graph.CreateNamespace("B", a);
        var c = _graph.CreateNamespace("C", b);
        _graph.CreateClass("X", c);
        _graph.CreateClass("Y", c);

        Build();

        var assemblyElement = _dsmModel.GetElementByFullname("Asm");
        Assert.That(assemblyElement.Children.Select(e => e.Name), Is.EquivalentTo(new[] { "A.B.C" }));
    }

    [Test]
    public void Build_TypeUnderMergedNamespace_KeepsItsOwnName()
    {
        // Only namespaces are ever merged: the parent of a type holds a type, so it is not a pass-through.
        var assembly = _graph.CreateAssembly("Asm");
        var a = _graph.CreateNamespace("A", assembly);
        var b = _graph.CreateNamespace("B", a);
        _graph.CreateClass("X", b);
        _graph.CreateClass("Y", b);

        Build();

        Assert.That(_dsmModel.GetElementByFullname("Asm.A.B.X"), Is.Not.Null);
    }

    [Test]
    public void Build_NamespaceWithSeveralChildren_IsKept()
    {
        var assembly = _graph.CreateAssembly("Asm");
        var ns = _graph.CreateNamespace("Ns", assembly);
        _graph.CreateClass("A", ns);
        _graph.CreateClass("B", ns);

        Build();

        Assert.That(_dsmModel.GetElementByFullname("Asm.Ns"), Is.Not.Null);
    }

    [Test]
    public void Build_NamespaceWithASingleType_IsKept()
    {
        // One child, but it is a type: the namespace does carry content and has to stay.
        var assembly = _graph.CreateAssembly("Asm");
        var ns = _graph.CreateNamespace("Ns", assembly);
        _graph.CreateClass("Only", ns);

        Build();

        Assert.That(_dsmModel.GetElementByFullname("Asm.Ns.Only"), Is.Not.Null);
    }

    [Test]
    public void Build_NamespaceBranchesIntoAnExternalType_IsNotCollapsed()
    {
        // Children are counted on the code graph, not on what reaches the model, so a namespace that
        // branches only into an external type keeps its level even though the matrix shows a single child.
        // The parser cannot produce this shape - ExternalCodeElementCache gives external elements their own
        // hierarchy up to the assembly and never hangs them under an internal namespace - so the case is
        // constructed here rather than observed. Pinned as the deliberate bound of the cheap check: if the
        // parser ever does mix the two, this is the behaviour that changes.
        var assembly = _graph.CreateAssembly("Asm");
        var outer = _graph.CreateNamespace("Outer", assembly);
        var inner = _graph.CreateNamespace("Inner", outer);
        _graph.CreateExternalClass("Ext", outer);
        _graph.CreateClass("A", inner);
        _graph.CreateClass("B", inner);

        Build();

        var assemblyElement = _dsmModel.GetElementByFullname("Asm");
        Assert.That(assemblyElement.Children.Select(e => e.Name), Is.EquivalentTo(new[] { "Outer" }));
    }

    [Test]
    public void Build_CalledTwice_DoesNotAccumulate()
    {
        _graph.CreateClass("A");

        Build();
        var countAfterFirst = AddedElementCount();
        Build();

        Assert.That(AddedElementCount(), Is.EqualTo(countAfterFirst));
    }
}
