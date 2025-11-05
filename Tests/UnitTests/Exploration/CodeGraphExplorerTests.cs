using CodeGraph.Exploration;
using CodeGraph.Graph;
using CodeParserTests.Helper;

namespace CodeParserTests.UnitTests.Exploration;

[TestFixture]
public class CodeGraphExplorerTests
{

    [SetUp]
    public void SetUp()
    {
        _graph = new TestCodeGraph();
        _explorer = new CodeGraphExplorer();
        _explorer.LoadCodeGraph(_graph);
    }

    private TestCodeGraph _graph = null!;
    private CodeGraphExplorer _explorer = null!;

    private Relationship Rel(CodeElement source, CodeElement target, RelationshipType type, RelationshipAttribute attributes = RelationshipAttribute.None)
    {
        var r = new Relationship(source.Id, target.Id, type, attributes);
        source.Relationships.Add(r);
        return r;
    }

    [Test]
    public void GetElements_ReturnsExisting_IgnoresMissing()
    {
        var a = _graph.CreateClass("A");
        var b = _graph.CreateMethod("B", a);
        var list = _explorer.GetElements(new List<string> { "A", "B", "C" });
        CollectionAssert.AreEquivalent(new[] { a, b }, list);
    }

    [Test]
    public void FindParents_ReturnsDistinctParents()
    {
        var cls = _graph.CreateClass("Cls");
        var m1 = _graph.CreateMethod("M1", cls);
        var m2 = _graph.CreateMethod("M2", cls);
        var result = _explorer.FindParents(new List<string> { m1.Id, m2.Id });
        CollectionAssert.AreEquivalent(new[] { cls }, result.Elements);
        Assert.That(result.Relationships, Is.Empty);
    }

    [Test]
    public void FindMissingTypesForLonelyTypeMembers_AddsContainers()
    {
        var asm = _graph.CreateAssembly("Asm");
        var ns = _graph.CreateNamespace("Ns", asm);
        var cls = _graph.CreateClass("Cls", ns);
        var m = _graph.CreateMethod("M", cls);
        var known = new HashSet<string> { m.Id }; // only method known
        var result = _explorer.FindMissingTypesForLonelyTypeMembers(known);
        Assert.That(result.Elements.Any(e => e.Id == cls.Id));
    }

    [Test]
    public void FindGapsInHierarchy_FillsIntermediateContainers()
    {
        var asm = _graph.CreateAssembly("Asm");
        var ns = _graph.CreateNamespace("Ns", asm);
        var cls = _graph.CreateClass("Cls", ns);
        var m = _graph.CreateMethod("M", cls);
        var known = new HashSet<string> { asm.Id, m.Id }; // gap: Ns, Cls missing
        var result = _explorer.FindGapsInHierarchy(known);
        Assert.That(result.Elements.Select(e => e.Id).ToHashSet().SetEquals(new[] { ns.Id, cls.Id }));
    }

    [Test]
    public void FindOutgoingCalls_DirectCalls()
    {
        var cls = _graph.CreateClass("Cls");
        var m1 = _graph.CreateMethod("M1", cls);
        var m2 = _graph.CreateMethod("M2", cls);
        Rel(m1, m2, RelationshipType.Calls);
        var result = _explorer.FindOutgoingCalls(m1.Id);
        CollectionAssert.AreEquivalent(new[] { m2 }, result.Elements);
        Assert.That(result.Relationships.Count(), Is.EqualTo(1));
    }

    [Test]
    public void FindIncomingCalls_DirectCallers()
    {
        var cls = _graph.CreateClass("Cls");
        var target = _graph.CreateMethod("Target", cls);
        var caller = _graph.CreateMethod("Caller", cls);
        Rel(caller, target, RelationshipType.Calls);
        var result = _explorer.FindIncomingCalls(target.Id);
        CollectionAssert.AreEquivalent(new[] { caller }, result.Elements);
        Assert.That(result.Relationships.Count(), Is.EqualTo(1));
    }

    [Test]
    public void FindIncomingCallsRecursive_TracesChain()
    {
        var cls = _graph.CreateClass("Cls");
        var m1 = _graph.CreateMethod("M1", cls);
        var m2 = _graph.CreateMethod("M2", cls);
        var m3 = _graph.CreateMethod("M3", cls);
        Rel(m2, m1, RelationshipType.Calls);
        Rel(m3, m2, RelationshipType.Calls);
        var result = _explorer.FindIncomingCallsRecursive(m1.Id);
        CollectionAssert.AreEquivalent(new[] { m2, m3 }, result.Elements);
        Assert.That(result.Relationships.Count(), Is.EqualTo(2));
    }

    [Test]
    public void FindOutgoingRelationships_ReturnsAllTargets()
    {
        var cls = _graph.CreateClass("Cls");
        var m1 = _graph.CreateMethod("M1", cls);
        var m2 = _graph.CreateMethod("M2", cls);
        Rel(m1, m2, RelationshipType.Calls);
        Rel(m1, m2, RelationshipType.Uses);
        var result = _explorer.FindOutgoingRelationships(m1.Id);
        CollectionAssert.AreEquivalent(new[] { m2, m2 }, result.Elements); // duplicates allowed
        Assert.That(result.Relationships.Count(), Is.EqualTo(2));
    }

    [Test]
    public void FindIncomingRelationships_ReturnsAllSources()
    {
        var cls = _graph.CreateClass("Cls");
        var m1 = _graph.CreateMethod("M1", cls);
        var m2 = _graph.CreateMethod("M2", cls);
        Rel(m2, m1, RelationshipType.Calls);
        Rel(m2, m1, RelationshipType.Uses);
        var result = _explorer.FindIncomingRelationships(m1.Id);
        CollectionAssert.AreEquivalent(new[] { m2, m2 }, result.Elements);
        Assert.That(result.Relationships.Count(), Is.EqualTo(2));
    }

    [Test]
    public void FindAllRelationships_FiltersByIdSet()
    {
        var cls = _graph.CreateClass("Cls");
        var a = _graph.CreateMethod("A", cls);
        var b = _graph.CreateMethod("B", cls);
        var c = _graph.CreateMethod("C", cls);
        var r1 = Rel(a, b, RelationshipType.Calls);
        Rel(b, c, RelationshipType.Calls);
        var set = new HashSet<string> { a.Id, b.Id }; // only r1 qualifies
        var rels = _explorer.FindAllRelationships(set).ToList();
        CollectionAssert.AreEquivalent(new[] { r1 }, rels);
    }

    [Test]
    public void FindOutgoingRelationshipsDeep_IncludesChildrenSources()
    {
        var cls = _graph.CreateClass("Cls");
        var m1 = _graph.CreateMethod("M1", cls);
        var m2 = _graph.CreateMethod("M2", cls);
        Rel(m1, m2, RelationshipType.Calls);
        var result = _explorer.FindOutgoingRelationshipsDeep(cls.Id);
        // Elements should include class + its two methods
        Assert.That(result.Elements.Any(e => e.Id == cls.Id));
        Assert.That(result.Elements.Any(e => e.Id == m1.Id));
        Assert.That(result.Elements.Any(e => e.Id == m2.Id));
        Assert.That(result.Relationships.Count(), Is.EqualTo(1));
    }

    [Test]
    public void FindIncomingRelationshipsDeep_IncludesChildrenTargets()
    {
        var cls = _graph.CreateClass("Cls");
        var m1 = _graph.CreateMethod("M1", cls);
        var m2 = _graph.CreateMethod("M2", cls);
        Rel(m2, m1, RelationshipType.Calls);
        var result = _explorer.FindIncomingRelationshipsDeep(cls.Id);
        // Should include both methods and relationship
        Assert.That(result.Elements.Any(e => e.Id == m1.Id));
        Assert.That(result.Elements.Any(e => e.Id == m2.Id));
        Assert.That(result.Relationships.Count(), Is.EqualTo(1));
    }

    [Test]
    public void FindFullInheritanceTree_ReturnsBaseAndDerived()
    {
        var baseCls = _graph.CreateClass("Base");
        var derived1 = _graph.CreateClass("D1");
        var derived2 = _graph.CreateClass("D2");
        Rel(derived1, baseCls, RelationshipType.Inherits);
        Rel(derived2, baseCls, RelationshipType.Inherits);
        var result = _explorer.FindFullInheritanceTree(baseCls.Id);
        CollectionAssert.AreEquivalent(new[] { baseCls, derived1, derived2 }, result.Elements);
        Assert.That(result.Relationships.Count(), Is.EqualTo(2));
    }

    [Test]
    public void FindSpecializations_ReturnsImplementorsAndOverrides()
    {
        var iface = _graph.CreateInterface("I");
        var impl = _graph.CreateClass("Impl");
        Rel(impl, iface, RelationshipType.Implements);
        var result = _explorer.FindSpecializations(iface.Id);
        CollectionAssert.AreEquivalent(new[] { impl }, result.Elements);
        Assert.That(result.Relationships.Count(), Is.EqualTo(1));
    }

    [Test]
    public void FindAbstractions_ReturnsInterfacesAndBases()
    {
        var iface = _graph.CreateInterface("I");
        var impl = _graph.CreateClass("Impl");
        Rel(impl, iface, RelationshipType.Implements);
        var result = _explorer.FindAbstractions(impl.Id);
        CollectionAssert.AreEquivalent(new[] { iface }, result.Elements);
        Assert.That(result.Relationships.Count(), Is.EqualTo(1));
    }

    [Test]
    public void FollowIncomingCallsHeuristically_RestrictsSideHierarchy()
    {
        var baseCls = _graph.CreateClass("Base");
        var s1 = _graph.CreateClass("S1");
        var s2 = _graph.CreateClass("S2");
        Rel(s1, baseCls, RelationshipType.Inherits);
        Rel(s2, baseCls, RelationshipType.Inherits);

        var mStart = _graph.CreateMethod("S1_M", s1);
        var callerAllowed = _graph.CreateMethod("S1_Caller", s1);
        var callerBase = _graph.CreateMethod("Base_Caller", baseCls);
        var callerForbidden = _graph.CreateMethod("S2_Caller", s2);

        Rel(callerAllowed, mStart, RelationshipType.Calls, RelationshipAttribute.IsInstanceCall);
        Rel(callerBase, mStart, RelationshipType.Calls);
        Rel(callerForbidden, mStart, RelationshipType.Calls);

        var result = _explorer.FollowIncomingCallsHeuristically(mStart.Id);
        var ids = result.Elements.Select(e => e.Id).ToHashSet();
        Assert.That(ids.Contains(callerAllowed.Id), Is.True);
        Assert.That(ids.Contains(callerBase.Id), Is.True);
        Assert.That(ids.Contains(callerForbidden.Id), Is.False, "Side hierarchy call should be excluded");
    }

    [Test]
    public void FollowIncomingCallsHeuristically_HandlesEvents()
    {
        var cls = _graph.CreateClass("Cls");
        var evt = _graph.CreateEvent("Evt", cls);
        var handler = _graph.CreateMethod("Handler", cls);
        Rel(handler, evt, RelationshipType.Handles);
        var raiser = _graph.CreateMethod("Raiser", cls);
        Rel(raiser, evt, RelationshipType.Invokes);
        var result = _explorer.FollowIncomingCallsHeuristically(handler.Id);
        var ids = result.Elements.Select(e => e.Id).ToHashSet();
        Assert.That(ids.Contains(evt.Id), Is.True);
        Assert.That(ids.Contains(raiser.Id), Is.True);
    }

    [Test]
    public void FindOutgoingRelationships_EmptyForUnknownId()
    {
        var result = _explorer.FindOutgoingRelationships("Unknown");
        Assert.That(result.Elements, Is.Empty);
        Assert.That(result.Relationships, Is.Empty);
    }

    [Test]
    public void FindIncomingCalls_ReturnsEmptyForUnknownId()
    {
        var result = _explorer.FindIncomingCalls("Unknown");
        Assert.That(result.Elements, Is.Empty);
        Assert.That(result.Relationships, Is.Empty);
    }

    [Test]
    public void FindFullInheritanceTree_ReturnsEmptyForUnknownId()
    {
        var result = _explorer.FindFullInheritanceTree("Unknown");
        Assert.That(result.Elements, Is.Empty);
        Assert.That(result.Relationships, Is.Empty);
    }
}