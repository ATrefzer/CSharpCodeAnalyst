using CodeGraph.Algorithms.Metrics;
using CodeGraph.Graph;
using CodeParserTests.Helper;

namespace CodeParserTests.UnitTests.Metrics;

[TestFixture]
public class HotspotAnalysisTests
{
    [SetUp]
    public void SetUp()
    {
        _graph = new TestCodeGraph();
    }

    private TestCodeGraph _graph = null!;

    private void Rel(CodeElement source, CodeElement target, RelationshipType type)
    {
        source.Relationships.Add(new Relationship(source.Id, target.Id, type));
    }

    [Test]
    public void Calculate_EmptyGraph_ReturnsEmpty()
    {
        var result = HotspotAnalysis.Calculate(_graph);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Calculate_MultipleCallsBetweenTypes_CountAsSingleEdge()
    {
        // A has two methods, both calling into B. Deduplicated this is one A->B type edge.
        var a = _graph.CreateClass("A");
        var m1 = _graph.CreateMethod("A.M1", a);
        var m2 = _graph.CreateMethod("A.M2", a);
        var b = _graph.CreateClass("B");
        var bm = _graph.CreateMethod("B.M", b);

        Rel(m1, bm, RelationshipType.Calls);
        Rel(m2, bm, RelationshipType.Calls);

        var result = HotspotAnalysis.Calculate(_graph).ToDictionary(h => h.Type.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result["A"].FanOut, Is.EqualTo(1), "Two calls collapse to one type edge");
            Assert.That(result["A"].FanIn, Is.EqualTo(0));
            Assert.That(result["B"].FanIn, Is.EqualTo(1));
            Assert.That(result["B"].FanOut, Is.EqualTo(0));
        });
    }

    [Test]
    public void Calculate_SelfCall_ProducesNoEdge()
    {
        // A method calling a sibling in the same class is internal coupling, not a hotspot signal.
        var a = _graph.CreateClass("A");
        var m1 = _graph.CreateMethod("A.M1", a);
        var m2 = _graph.CreateMethod("A.M2", a);

        Rel(m1, m2, RelationshipType.Calls);

        var result = HotspotAnalysis.Calculate(_graph).ToDictionary(h => h.Type.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result["A"].FanIn, Is.EqualTo(0));
            Assert.That(result["A"].FanOut, Is.EqualTo(0));
        });
    }

    [Test]
    public void Calculate_ExternalType_ExcludedFromResultAndEdges()
    {
        var a = _graph.CreateClass("A");
        var external = new CodeElement("Ext", CodeElementType.Class, "Ext", "Ext", null) { IsExternal = true };
        _graph.Nodes["Ext"] = external;

        Rel(a, external, RelationshipType.Uses);

        var result = HotspotAnalysis.Calculate(_graph);

        Assert.Multiple(() =>
        {
            Assert.That(result.Select(h => h.Type.Id), Does.Not.Contain("Ext"));
            Assert.That(result.Single(h => h.Type.Id == "A").FanOut, Is.EqualTo(0),
                "Edge to external type is dropped");
        });
    }

    [Test]
    public void Calculate_HandlesContainmentAndBundled_AreNotCountedAsDependencies()
    {
        var a = _graph.CreateClass("A");
        var b = _graph.CreateClass("B");
        var c = _graph.CreateClass("C");

        // Handles is stored handler -> event but is reversed callback wiring, not a dependency.
        Rel(a, b, RelationshipType.Handles);
        // Containment is the hierarchy, Bundled is an artificial UI edge.
        Rel(a, c, RelationshipType.Containment);
        Rel(b, c, RelationshipType.Bundled);

        var result = HotspotAnalysis.Calculate(_graph).ToDictionary(h => h.Type.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result["A"].FanOut, Is.EqualTo(0));
            Assert.That(result["B"].FanIn, Is.EqualTo(0));
            Assert.That(result["C"].FanIn, Is.EqualTo(0));
        });
    }

    [Test]
    public void Calculate_Inheritance_MakesTheBaseTypeTheHotspot()
    {
        // Derived depends on Base. PageRank must flow to the base type.
        var baseType = _graph.CreateClass("Base");
        var derived1 = _graph.CreateClass("Derived1");
        var derived2 = _graph.CreateClass("Derived2");

        Rel(derived1, baseType, RelationshipType.Inherits);
        Rel(derived2, baseType, RelationshipType.Inherits);

        var result = HotspotAnalysis.Calculate(_graph);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].Type.Id, Is.EqualTo("Base"));
            Assert.That(result.Single(h => h.Type.Id == "Base").FanIn, Is.EqualTo(2));
        });
    }

    [Test]
    public void Calculate_HubType_RanksAboveItsCallers()
    {
        // A, B, C all depend on Hub. Hub is depended upon by everything -> highest PageRank.
        var hub = _graph.CreateClass("Hub");
        var a = _graph.CreateClass("A");
        var b = _graph.CreateClass("B");
        var c = _graph.CreateClass("C");

        Rel(a, hub, RelationshipType.Uses);
        Rel(b, hub, RelationshipType.Uses);
        Rel(c, hub, RelationshipType.Uses);

        var result = HotspotAnalysis.Calculate(_graph);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].Type.Id, Is.EqualTo("Hub"));
            Assert.That(result[0].Rank, Is.EqualTo(1));
            Assert.That(result.Single(h => h.Type.Id == "Hub").FanIn, Is.EqualTo(3));
            // Normalized score of the average type is 1.0; the hub must be clearly above average.
            Assert.That(result[0].Score, Is.GreaterThan(1.0));
        });
    }

    [Test]
    public void Calculate_TransitiveImportance_BeatsRawFanIn()
    {
        // "Utility" is used by three trivial leaves (raw fan-in 3).
        // "Core" is used only by "Gateway" (raw fan-in 1), but Gateway is itself the entry
        // point everything funnels through. PageRank should rate Core at least as high as the
        // popular-but-peripheral Utility, which raw fan-in alone would miss.
        var utility = _graph.CreateClass("Utility");
        var leaf1 = _graph.CreateClass("Leaf1");
        var leaf2 = _graph.CreateClass("Leaf2");
        var leaf3 = _graph.CreateClass("Leaf3");
        Rel(leaf1, utility, RelationshipType.Uses);
        Rel(leaf2, utility, RelationshipType.Uses);
        Rel(leaf3, utility, RelationshipType.Uses);

        var core = _graph.CreateClass("Core");
        var gateway = _graph.CreateClass("Gateway");
        Rel(leaf1, gateway, RelationshipType.Uses);
        Rel(leaf2, gateway, RelationshipType.Uses);
        Rel(leaf3, gateway, RelationshipType.Uses);
        Rel(gateway, core, RelationshipType.Uses);

        var result = HotspotAnalysis.Calculate(_graph).ToDictionary(h => h.Type.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result["Utility"].FanIn, Is.EqualTo(3));
            Assert.That(result["Core"].FanIn, Is.EqualTo(1));
            // Core inherits importance from the heavily-used Gateway.
            Assert.That(result["Core"].PageRank, Is.GreaterThanOrEqualTo(result["Utility"].PageRank));
        });
    }

    [Test]
    public void Calculate_PageRankValues_FormProbabilityDistribution()
    {
        var a = _graph.CreateClass("A");
        var b = _graph.CreateClass("B");
        var c = _graph.CreateClass("C");
        Rel(a, b, RelationshipType.Uses);
        Rel(b, c, RelationshipType.Uses);

        var result = HotspotAnalysis.Calculate(_graph);

        Assert.That(result.Sum(h => h.PageRank), Is.EqualTo(1.0).Within(1e-6));
    }
}
