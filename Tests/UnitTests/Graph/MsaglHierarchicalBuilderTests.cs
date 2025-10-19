using Contracts.Graph;
using CSharpCodeAnalyst.Areas.GraphArea;
using CSharpCodeAnalyst.Areas.GraphArea.Filtering;

namespace CodeParserTests.UnitTests.Graph;

[TestFixture]
public class MsaglHierarchicalBuilderTests
{
    private CodeGraph BuildHierarchy()
    {
        var g = new CodeGraph();
        var asm = new CodeElement("Asm", CodeElementType.Assembly, "Asm", "Asm", null);
        var ns = new CodeElement("Ns", CodeElementType.Namespace, "Ns", "Ns", asm);
        var cls = new CodeElement("Cls", CodeElementType.Class, "Cls", "Ns.Cls", ns);
        var methodA = new CodeElement("M1", CodeElementType.Method, "M1", "Ns.Cls.M1", cls);
        var methodB = new CodeElement("M2", CodeElementType.Method, "M2", "Ns.Cls.M2", cls);
        asm.Children.Add(ns);
        ns.Children.Add(cls);
        cls.Children.Add(methodA);
        cls.Children.Add(methodB);
        g.Nodes[asm.Id] = asm;
        g.Nodes[ns.Id] = ns;
        g.Nodes[cls.Id] = cls;
        g.Nodes[methodA.Id] = methodA;
        g.Nodes[methodB.Id] = methodB;
        // Relationships M1 -> M2
        methodA.Relationships.Add(new Relationship(methodA.Id, methodB.Id, RelationshipType.Calls));
        return g;
    }

    private (MsaglHierarchicalBuilder builder, PresentationState state, GraphHideFilter filter) CreateBuilder()
    {
        var state = new PresentationState();
        var builder = new MsaglHierarchicalBuilder();
        var filter = new GraphHideFilter();
        return (builder, state, filter);
    }

    [Test]
    public void CollapsedClass_HidesChildren()
    {
        var g = BuildHierarchy();
        var (builder, state, filter) = CreateBuilder();
        state.SetCollapsedState("Cls", true); // collapse class -> methods hidden
        var msagl = builder.CreateGraph(g, state, false, filter);

        // Graph should have nodes: Asm, Ns, Cls only
        var ids = msagl.Nodes.Select(n => n.Id).ToHashSet();
        CollectionAssert.AreEquivalent(new[] { "Cls" }, ids);

        // Nodes with children are subgraphs: Asm, Ns, Cls
        var subGraphs = msagl.SubgraphMap.Values.Select(s => s.LabelText);
        CollectionAssert.AreEquivalent(new[] { "Asm", "Ns", "the root subgraph's boundary" }, subGraphs);
    }

    [Test]
    public void ExpandedClass_ShowsChildren()
    {
        var g = BuildHierarchy();
        var (builder, state, filter) = CreateBuilder();
        var msagl = builder.CreateGraph(g, state, false, filter);

        var ids = msagl.Nodes.Select(n => n.Id).ToHashSet();
        CollectionAssert.AreEquivalent(new[] { "M1", "M2" }, ids);

        // Nodes with children are subgraphs: Asm, Ns, Cls
        var subGraphs = msagl.SubgraphMap.Values.Select(s => s.LabelText);
        CollectionAssert.AreEquivalent(new[] { "Asm", "Ns", "Cls", "the root subgraph's boundary" }, subGraphs);
    }

    [Test]
    public void EdgeBundling_WhenCollapsed()
    {
        var g = BuildHierarchy();
        var (builder, state, filter) = CreateBuilder();
        // Add second edge M2->M1 so when collapsed they bundle between Cls and Cls (self) -> removed
        var m2 = g.Nodes["M2"];
        var m1 = g.Nodes["M1"];
        m2.Relationships.Add(new Relationship(m2.Id, m1.Id, RelationshipType.Calls));
        state.SetCollapsedState("Cls", true);
        var msagl = builder.CreateGraph(g, state, false, filter);
        // No edge expected because both directions collapse into same container
        Assert.That(msagl.Edges.Count(), Is.EqualTo(0));
    }

    [Test]
    public void InformationFlow_ReversesHandlesOverridesImplements()
    {
        var g = new CodeGraph();
        var clsA = new CodeElement("A", CodeElementType.Class, "A", "A", null);
        var clsB = new CodeElement("B", CodeElementType.Class, "B", "B", null);
        g.Nodes[clsA.Id] = clsA;
        g.Nodes[clsB.Id] = clsB;
        clsA.Relationships.Add(new Relationship(clsA.Id, clsB.Id, RelationshipType.Overrides));
        var (builder, state, filter) = CreateBuilder();
        var graphNormal = builder.CreateGraph(g, state, false, filter);
        var edgeNormal = graphNormal.Edges.First();
        Assert.That(edgeNormal.Source, Is.EqualTo("A"));
        Assert.That(edgeNormal.Target, Is.EqualTo("B"));
        var graphFlow = builder.CreateGraph(g, state, true, filter);
        var edgeFlow = graphFlow.Edges.First();
        Assert.That(edgeFlow.Source, Is.EqualTo("B"));
        Assert.That(edgeFlow.Target, Is.EqualTo("A"));
    }

    [Test]
    public void HideFilter_ExcludesHiddenElementsAndRelationships()
    {
        var g = BuildHierarchy();
        var (builder, state, filter) = CreateBuilder();
        filter.HiddenElementTypes.Add(CodeElementType.Method);
        var msagl = builder.CreateGraph(g, state, false, filter);
        var ids = msagl.Nodes.Select(n => n.Id).ToHashSet();
        Assert.That(ids.Contains("M1"), Is.False);
        Assert.That(ids.Contains("M2"), Is.False);
    }
}