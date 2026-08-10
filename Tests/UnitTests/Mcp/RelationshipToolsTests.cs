using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Mcp.Tools;

namespace CodeParserTests.UnitTests.Mcp;

/// <summary>
///     Tests for <see cref="RelationshipTools" />.
///     <para>
///         The fixture is the smallest arrangement that still has every trap in it: a class whose
///         members call each other (internal), a member calling out of the class (crossing), and a
///         caller that only reaches an implementation through an interface.
///     </para>
///     <code>
///     OrderController.Post  --Calls-->      IOrderService.Place
///     OrderService.Place    --Implements--> IOrderService.Place
///     OrderService.Place    --Calls-->      OrderService.Validate   (internal to OrderService)
///     OrderService.Place    --Calls-->      OrderRepository.Save    (leaves OrderService)
///     </code>
/// </summary>
[TestFixture]
public class RelationshipToolsTests
{
    [SetUp]
    public void SetUp()
    {
        _graph = new TestCodeGraph();
        var assembly = _graph.CreateAssembly("Sample.Core");
        var ns = _graph.CreateNamespace("Services", assembly);

        _contract = _graph.CreateInterface("IOrderService", ns);
        _contractPlace = _graph.CreateMethod("IOrderService.Place", _contract);

        _orderService = _graph.CreateClass("OrderService", ns);
        _place = _graph.CreateMethod("OrderService.Place", _orderService);
        _validate = _graph.CreateMethod("OrderService.Validate", _orderService);

        _repository = _graph.CreateClass("OrderRepository", ns);
        _save = _graph.CreateMethod("OrderRepository.Save", _repository);

        _controller = _graph.CreateClass("OrderController", ns);
        _post = _graph.CreateMethod("OrderController.Post", _controller);

        Link(_place, _contractPlace, RelationshipType.Implements);
        Link(_place, _validate, RelationshipType.Calls);
        Link(_place, _save, RelationshipType.Calls);
        Link(_post, _contractPlace, RelationshipType.Calls);

        _tools = new RelationshipTools(FakeSnapshotSource.With(_graph));
    }

    private TestCodeGraph _graph = null!;
    private CodeElement _contract = null!;
    private CodeElement _contractPlace = null!;
    private CodeElement _orderService = null!;
    private CodeElement _place = null!;
    private CodeElement _validate = null!;
    private CodeElement _repository = null!;
    private CodeElement _save = null!;
    private CodeElement _controller = null!;
    private CodeElement _post = null!;
    private RelationshipTools _tools = null!;

    private static void Link(CodeElement source, CodeElement target, RelationshipType type)
    {
        source.Relationships.Add(new Relationship(source.Id, target.Id, type));
    }

    [Test]
    public async Task Outgoing_ListsWhatTheElementItselfDependsOn()
    {
        var answer = await _tools.FindOutgoingRelationshipsAsync(_place.Id);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("3 relationship(s)"));
            Assert.That(answer, Does.Contain(_validate.Id));
            Assert.That(answer, Does.Contain(_save.Id));
            Assert.That(answer, Does.Contain(_contractPlace.Id));
        });
    }

    /// <summary>
    ///     The semantics that is easiest to get wrong. "deep" means relationships crossing the
    ///     element's boundary, not everything inside it: Place calling Validate stays within
    ///     OrderService and is deliberately absent. Without the note in the answer a caller would
    ///     conclude that call does not exist.
    /// </summary>
    [Test]
    public async Task OutgoingDeep_ReportsWhatLeavesTheClassButNotWhatStaysInside()
    {
        var answer = await _tools.FindOutgoingRelationshipsAsync(_orderService.Id, true);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain(_save.Id), "the call leaving the class");
            Assert.That(answer, Does.Contain(_contractPlace.Id), "the interface it implements");
            Assert.That(answer, Does.Not.Contain(_validate.Id), "internal call must not be listed");
            Assert.That(answer, Does.Contain("internal and not shown"),
                "the absence has to be explained, or it reads as a finding");
        });
    }

    [Test]
    public async Task Outgoing_WithoutDeep_DoesNotDescendIntoMembers()
    {
        var answer = await _tools.FindOutgoingRelationshipsAsync(_orderService.Id);

        Assert.That(answer, Does.Contain("No relationships found"));
    }

    [Test]
    public async Task Incoming_ListsWhatDependsOnTheElement()
    {
        var answer = await _tools.FindIncomingRelationshipsAsync(_contractPlace.Id);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain(_post.Id), "the caller");
            Assert.That(answer, Does.Contain(_place.Id), "the implementation");
        });
    }

    /// <summary>
    ///     Nothing calls the concrete method directly - every caller arrives through the interface. The
    ///     answer is formally correct and practically misleading, so it has to say what it does not
    ///     prove.
    /// </summary>
    [Test]
    public async Task IncomingCalls_WithoutAbstractions_MissesCallersGoingThroughAnInterface()
    {
        var answer = await _tools.FindIncomingCallsAsync(_place.Id, false);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("No callers found"));
            Assert.That(answer, Does.Contain("does not prove the method is unused"));
        });
    }

    [Test]
    public async Task IncomingCalls_WithAbstractions_FindsTheCallerThroughTheInterface()
    {
        var answer = await _tools.FindIncomingCallsAsync(_place.Id);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain(_post.Id));
            Assert.That(answer, Does.Contain("Heuristic"));
        });
    }

    [Test]
    public async Task IncomingCalls_DoesNotReportTheMethodAsItsOwnCaller()
    {
        Link(_validate, _place, RelationshipType.Calls);

        var answer = await _tools.FindIncomingCallsAsync(_place.Id, false);

        Assert.That(answer, Does.Contain(_validate.Id));
        Assert.That(answer, Does.Contain("1 caller(s)"));
    }

    /// <summary>
    ///     Both ends are expanded to their contents first, so asking about two classes has to surface
    ///     the chain between their methods - that is the whole point of the tool.
    /// </summary>
    [Test]
    public async Task PathsBetween_TwoClasses_FindsTheChainBetweenTheirMembers()
    {
        var answer = await _tools.FindPathsBetweenAsync(_orderService.Id, _repository.Id);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("OrderService.Place"));
            Assert.That(answer, Does.Contain("--Calls-->"));
            Assert.That(answer, Does.Contain("OrderRepository.Save"));
        });
    }

    [Test]
    public async Task PathsBetween_UnconnectedElements_SaysWhatThatDoesAndDoesNotMean()
    {
        var lonely = _graph.CreateClass("Unrelated");

        var answer = await _tools.FindPathsBetweenAsync(_orderService.Id, lonely.Id);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("No dependency chain"));
            Assert.That(answer, Does.Contain("raise maxLength"));
            Assert.That(answer, Does.Contain("shared parent"));
        });
    }

    /// <summary>
    ///     Containment is not a dependency. If it were, every two elements under the same assembly
    ///     would be "connected" and the tool would answer nothing useful ever again.
    /// </summary>
    [Test]
    public async Task PathsBetween_SiblingsWithNoDependency_AreNotConnectedThroughTheirParent()
    {
        var answer = await _tools.FindPathsBetweenAsync(_controller.Id, _repository.Id);

        Assert.That(answer, Does.Contain("No dependency chain"));
    }

    [Test]
    public async Task AllTools_WithAnUnknownId_ExplainInsteadOfFailing()
    {
        var outgoing = await _tools.FindOutgoingRelationshipsAsync("nope");
        var incoming = await _tools.FindIncomingRelationshipsAsync("nope");
        var calls = await _tools.FindIncomingCallsAsync("nope");
        var paths = await _tools.FindPathsBetweenAsync("nope", _place.Id);

        Assert.Multiple(() =>
        {
            Assert.That(outgoing, Does.Contain("search_elements"));
            Assert.That(incoming, Does.Contain("search_elements"));
            Assert.That(calls, Does.Contain("search_elements"));
            Assert.That(paths, Does.Contain("search_elements"));
        });
    }

    [Test]
    public async Task AllTools_WithoutAProject_SaySo()
    {
        var tools = new RelationshipTools(FakeSnapshotSource.Empty());

        Assert.Multiple(async () =>
        {
            Assert.That(await tools.FindOutgoingRelationshipsAsync("x"), Does.Contain("No project"));
            Assert.That(await tools.FindIncomingRelationshipsAsync("x"), Does.Contain("No project"));
            Assert.That(await tools.FindIncomingCallsAsync("x"), Does.Contain("No project"));
            Assert.That(await tools.FindPathsBetweenAsync("x", "y"), Does.Contain("No project"));
        });
    }
}
