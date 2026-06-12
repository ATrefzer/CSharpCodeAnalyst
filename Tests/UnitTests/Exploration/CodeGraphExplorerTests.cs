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
        var list = _explorer.GetElements(["A", "B", "C"]);
        Assert.That(list, Is.EquivalentTo([a, b]));
    }

    [Test]
    public void FindParents_ReturnsDistinctParents()
    {
        var cls = _graph.CreateClass("Cls");
        var m1 = _graph.CreateMethod("M1", cls);
        var m2 = _graph.CreateMethod("M2", cls);
        var result = _explorer.FindParents([m1.Id, m2.Id]);
        Assert.That(result.Elements, Is.EquivalentTo([cls]));
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
        Assert.That(result.Elements.Select(e => e.Id).ToHashSet().SetEquals([ns.Id, cls.Id]));
    }

    [Test]
    public void FindOutgoingCalls_DirectCalls()
    {
        var cls = _graph.CreateClass("Cls");
        var m1 = _graph.CreateMethod("M1", cls);
        var m2 = _graph.CreateMethod("M2", cls);
        Rel(m1, m2, RelationshipType.Calls);
        var result = _explorer.FindOutgoingCalls(m1.Id);
        Assert.That(result.Elements, Is.EquivalentTo([m2]));
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
        Assert.That(result.Elements, Is.EquivalentTo([caller]));
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
        Assert.That(result.Elements, Is.EquivalentTo([m2, m3]));
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
        Assert.That(result.Elements, Is.EquivalentTo([m2, m2])); // duplicates allowed
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
        Assert.That(result.Elements, Is.EquivalentTo([m2, m2]));
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
        Assert.That(rels, Is.EquivalentTo([r1]));
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
        Assert.That(result.Elements, Is.EquivalentTo([baseCls, derived1, derived2]));
        Assert.That(result.Relationships.Count(), Is.EqualTo(2));
    }

    [Test]
    public void FindSpecializations_ReturnsImplementorsAndOverrides()
    {
        var iface = _graph.CreateInterface("I");
        var impl = _graph.CreateClass("Impl");
        Rel(impl, iface, RelationshipType.Implements);
        var result = _explorer.FindSpecializations(iface.Id);
        Assert.That(result.Elements, Is.EquivalentTo([impl]));
        Assert.That(result.Relationships.Count(), Is.EqualTo(1));
    }

    [Test]
    public void FindAbstractions_ReturnsInterfacesAndBases()
    {
        var iface = _graph.CreateInterface("I");
        var impl = _graph.CreateClass("Impl");
        Rel(impl, iface, RelationshipType.Implements);
        var result = _explorer.FindAbstractions(impl.Id);
        Assert.That(result.Elements, Is.EquivalentTo([iface]));
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
    public void FollowIncomingCallsHeuristically_ExcludesThisCallFromSideHierarchy()
    {
        // this.M() in a side hierarchy dispatches to the runtime type of "this",
        // which is always inside that side hierarchy. It can never reach S1_M.
        var baseCls = _graph.CreateClass("Base");
        var s1 = _graph.CreateClass("S1");
        var s2 = _graph.CreateClass("S2");
        Rel(s1, baseCls, RelationshipType.Inherits);
        Rel(s2, baseCls, RelationshipType.Inherits);

        var baseM = _graph.CreateMethod("Base_M", baseCls);
        var mStart = _graph.CreateMethod("S1_M", s1);
        Rel(mStart, baseM, RelationshipType.Overrides);

        var thisCaller = _graph.CreateMethod("S2_ThisCaller", s2);
        Rel(thisCaller, baseM, RelationshipType.Calls, RelationshipAttribute.IsThisCall);

        var baseCaller = _graph.CreateMethod("Base_Caller", baseCls);
        Rel(baseCaller, baseM, RelationshipType.Calls);

        var result = _explorer.FollowIncomingCallsHeuristically(mStart.Id);
        var ids = result.Elements.Select(e => e.Id).ToHashSet();
        Assert.That(ids.Contains(baseCaller.Id), Is.True, "Implicit call within own hierarchy must stay");
        Assert.That(ids.Contains(thisCaller.Id), Is.False, "this-call from side hierarchy cannot dispatch to S1_M");
    }

    [Test]
    public void FollowIncomingCallsHeuristically_KeepsHierarchyRestrictionAfterStaticCall()
    {
        // Target is reached via a static call from the virtual method WorkerA_Work.
        // Implicit calls to WorkerBase_Work from the sibling WorkerB can never
        // dispatch to WorkerA_Work and must be excluded.
        var util = _graph.CreateClass("Util");
        var target = _graph.CreateMethod("Util_Log", util);

        var workerBase = _graph.CreateClass("WorkerBase");
        var workerA = _graph.CreateClass("WorkerA");
        var workerB = _graph.CreateClass("WorkerB");
        Rel(workerA, workerBase, RelationshipType.Inherits);
        Rel(workerB, workerBase, RelationshipType.Inherits);

        var baseWork = _graph.CreateMethod("WorkerBase_Work", workerBase);
        var aWork = _graph.CreateMethod("WorkerA_Work", workerA);
        Rel(aWork, baseWork, RelationshipType.Overrides);
        Rel(aWork, target, RelationshipType.Calls, RelationshipAttribute.IsStaticCall);

        var drive = _graph.CreateMethod("WorkerBase_Drive", workerBase);
        Rel(drive, baseWork, RelationshipType.Calls);

        var bOther = _graph.CreateMethod("WorkerB_Other", workerB);
        Rel(bOther, baseWork, RelationshipType.Calls);

        var result = _explorer.FollowIncomingCallsHeuristically(target.Id);
        var ids = result.Elements.Select(e => e.Id).ToHashSet();
        Assert.That(ids.Contains(drive.Id), Is.True, "Implicit call within own hierarchy must stay");
        Assert.That(ids.Contains(bOther.Id), Is.False, "Implicit call from sibling cannot dispatch to WorkerA_Work");
    }

    [Test]
    public void FollowIncomingCallsHeuristically_FollowsChainThroughProperty()
    {
        var repo = _graph.CreateClass("Repo");
        var compute = _graph.CreateMethod("Repo_Compute", repo);

        var facade = _graph.CreateClass("Facade");
        var valueProp = _graph.CreateProperty("Facade_Value", facade);
        Rel(valueProp, compute, RelationshipType.Calls, RelationshipAttribute.IsInstanceCall);

        var client = _graph.CreateClass("Client");
        var consume = _graph.CreateMethod("Client_Consume", client);
        Rel(consume, valueProp, RelationshipType.Calls, RelationshipAttribute.IsInstanceCall);

        var result = _explorer.FollowIncomingCallsHeuristically(compute.Id);
        var ids = result.Elements.Select(e => e.Id).ToHashSet();
        Assert.That(ids.Contains(valueProp.Id), Is.True);
        Assert.That(ids.Contains(consume.Id), Is.True, "Chain must continue through the property getter");
    }

    [Test]
    public void FollowIncomingCallsHeuristically_PublisherSideIsNotFilteredBySubscriberHierarchy()
    {
        // Subscriber and Publisher share a base class, so the start context forbids Publisher.
        // Raising an event dispatches via delegate; the implicit call Trigger -> Raise on the
        // publisher side is a real origin and must not be filtered.
        var baseCls = _graph.CreateClass("Base");
        var sub = _graph.CreateClass("Subscriber");
        var pub = _graph.CreateClass("Publisher");
        Rel(sub, baseCls, RelationshipType.Inherits);
        Rel(pub, baseCls, RelationshipType.Inherits);

        var onChanged = _graph.CreateMethod("Subscriber_OnChanged", sub);
        var changed = _graph.CreateEvent("Publisher_Changed", pub);
        Rel(onChanged, changed, RelationshipType.Handles);

        var raise = _graph.CreateMethod("Publisher_Raise", pub);
        Rel(raise, changed, RelationshipType.Invokes);

        var trigger = _graph.CreateMethod("Publisher_Trigger", pub);
        Rel(trigger, raise, RelationshipType.Calls);

        var result = _explorer.FollowIncomingCallsHeuristically(onChanged.Id);
        var ids = result.Elements.Select(e => e.Id).ToHashSet();
        Assert.That(ids.Contains(raise.Id), Is.True);
        Assert.That(ids.Contains(trigger.Id), Is.True,
            "The implicit call on the publisher side must not be filtered by the subscriber's hierarchy");
    }

    [Test]
    public void FollowIncomingCallsHeuristically_DefaultInterfaceMethodCallerIsNotFiltered()
    {
        // IBase is implemented by Base and therefore part of the expanded hierarchy.
        // An implicit call from a default interface method can dispatch to any implementing
        // class, including S1, and must not be filtered.
        var iface = _graph.CreateInterface("IBase");
        var baseCls = _graph.CreateClass("Base");
        var s1 = _graph.CreateClass("S1");
        var s2 = _graph.CreateClass("S2");
        Rel(baseCls, iface, RelationshipType.Implements);
        Rel(s1, baseCls, RelationshipType.Inherits);
        Rel(s2, baseCls, RelationshipType.Inherits);

        var baseM = _graph.CreateMethod("Base_M", baseCls);
        var mStart = _graph.CreateMethod("S1_M", s1);
        Rel(mStart, baseM, RelationshipType.Overrides);

        var dimCaller = _graph.CreateMethod("IBase_DefaultMethod", iface);
        Rel(dimCaller, baseM, RelationshipType.Calls);

        var sideCaller = _graph.CreateMethod("S2_Caller", s2);
        Rel(sideCaller, baseM, RelationshipType.Calls);

        var result = _explorer.FollowIncomingCallsHeuristically(mStart.Id);
        var ids = result.Elements.Select(e => e.Id).ToHashSet();
        Assert.That(ids.Contains(sideCaller.Id), Is.False, "Implicit call from sibling class is still filtered");
        Assert.That(ids.Contains(dimCaller.Id), Is.True,
            "A default interface method may execute on any implementer, including S1");
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