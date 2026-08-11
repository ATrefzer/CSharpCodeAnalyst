using System.Globalization;
using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Mcp.Tools;

namespace CodeParserTests.UnitTests.Mcp;

/// <summary>
///     Tests for <see cref="ElementTools" />: finding an element and describing it.
///     <para>
///         Several of these pin behaviour that the tool's own description promises. That description is
///         the only thing a language model ever learns about the tool - it cannot read the code and it
///         cannot ask - so a claim in it that the code does not honour is a defect, and these tests are
///         what catches it.
///     </para>
/// </summary>
[TestFixture]
public class ElementToolsTests
{
    [SetUp]
    public void SetUp()
    {
        _graph = new TestCodeGraph();
        _assembly = _graph.CreateAssembly("Sample.Core");
        _namespace = _graph.CreateNamespace("Services", _assembly);
        _orderService = _graph.CreateClass("OrderService", _namespace, accessLevel: AccessLevel.Public);
        _place = _graph.CreateMethod("Place", _orderService);
        _validate = _graph.CreateMethod("Validate", _orderService);
        _tools = new ElementTools(FakeSnapshotSource.With(_graph));
    }

    private TestCodeGraph _graph = null!;
    private CodeElement _assembly = null!;
    private CodeElement _namespace = null!;
    private CodeElement _orderService = null!;
    private CodeElement _place = null!;
    private CodeElement _validate = null!;
    private ElementTools _tools = null!;

    [Test]
    public async Task Search_LowerCaseTerm_MatchesCaseInsensitively()
    {
        var answer = await _tools.SearchElementsAsync("orderservice");

        Assert.That(answer, Does.Contain("OrderService"));
    }

    /// <summary>
    ///     Camel-hump matching, as promised by the tool description: the term is split at every
    ///     uppercase letter and the parts must occur in order.
    /// </summary>
    [Test]
    public async Task Search_CamelHumpTerm_FindsTheType()
    {
        var answer = await _tools.SearchElementsAsync("OS");

        Assert.That(answer, Does.Contain("OrderService"));
    }

    /// <summary>
    ///     The counterpart to the test above, and the reason it exists. The parts of a camel-hump term
    ///     are literal, not abbreviations - "Svc" simply does not occur in "OrderService". An earlier
    ///     version of the tool description claimed otherwise, which would have sent a caller looking
    ///     for elements it could never find.
    /// </summary>
    [Test]
    public async Task Search_CamelHumpWithAnAbbreviationThatDoesNotOccur_FindsNothing()
    {
        var answer = await _tools.SearchElementsAsync("OSvc");

        Assert.That(answer, Does.Contain("Nothing matches"));
    }

    [Test]
    public async Task Search_TypeFilter_RestrictsTheKind()
    {
        var answer = await _tools.SearchElementsAsync("type:method");

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("Place"));
            Assert.That(answer, Does.Contain("Validate"));
            Assert.That(answer, Does.Not.Contain("[Class]"));
        });
    }

    [Test]
    public async Task Search_NegatedTerm_Excludes()
    {
        var answer = await _tools.SearchElementsAsync("type:method -Place");

        // The header echoes the query, so "Place" appears in the answer either way - only the result
        // list can tell whether the exclusion worked.
        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("1 match(es)"));
            Assert.That(answer, Does.Contain($"id={_validate.Id}"));
            Assert.That(answer, Does.Not.Contain($"id={_place.Id}"));
        });
    }

    /// <summary>
    ///     The expression only says yes or no. Without a ranking the type a caller asked for would sit
    ///     below its own members and anything else whose full name happens to contain the word.
    /// </summary>
    [Test]
    public async Task Search_ExactNameMatch_IsListedFirst()
    {
        var extra = _graph.CreateClass("OrderServiceFactory", _namespace);

        var answer = await _tools.SearchElementsAsync("OrderService");

        var exact = answer.IndexOf(_orderService.Id, StringComparison.Ordinal);
        var other = answer.IndexOf(extra.Id, StringComparison.Ordinal);
        Assert.That(exact, Is.GreaterThanOrEqualTo(0));
        Assert.That(other, Is.GreaterThan(exact));
    }

    /// <summary>
    ///     A truncated list that does not say it is truncated is worse than a short one: the caller
    ///     concludes there are only as many results as it can see.
    /// </summary>
    [Test]
    public async Task Search_MoreResultsThanTheLimit_SaysHowManyAreMissing()
    {
        var answer = await _tools.SearchElementsAsync("type:method", 1);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("2 match(es)"));
            Assert.That(answer, Does.Contain("1 more not shown"));
        });
    }

    [Test]
    public async Task Search_EmptyQuery_AsksForOne()
    {
        var answer = await _tools.SearchElementsAsync("   ");

        Assert.That(answer, Does.Contain("query is empty"));
    }

    [Test]
    public async Task Search_WithoutAProject_SaysSo()
    {
        var tools = new ElementTools(FakeSnapshotSource.Empty());

        var answer = await tools.SearchElementsAsync("anything");

        Assert.That(answer, Does.Contain("No project is loaded"));
    }

    /// <summary>
    ///     A stale id and a deleted element look identical to the caller, and the recovery differs, so
    ///     the answer has to name both possibilities.
    /// </summary>
    [Test]
    public async Task Describe_UnknownId_ExplainsThatIdsDoNotSurviveAReparse()
    {
        var answer = await _tools.DescribeElementAsync("no-such-id");

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("no-such-id"));
            Assert.That(answer, Does.Contain("re-parsed"));
            Assert.That(answer, Does.Contain("search_elements"));
        });
    }

    [Test]
    public async Task Describe_ListsContainersAndContents()
    {
        var answer = await _tools.DescribeElementAsync(_orderService.Id);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("[Class] OrderService"));
            Assert.That(answer, Does.Contain("Contained in:"));
            Assert.That(answer, Does.Contain("Sample.Core"));
            Assert.That(answer, Does.Contain("Contains (2)"));
            Assert.That(answer, Does.Contain(_place.Id));
            Assert.That(answer, Does.Contain(_validate.Id));
        });
    }

    /// <summary>
    ///     The children of a large container do not fit, and the caller has no tool that pages through
    ///     them - so the truncation notice has to name the one query that reaches the rest. This asserts
    ///     that the notice is there; the test below asserts that following it actually works.
    /// </summary>
    [Test]
    public async Task Describe_MoreChildrenThanTheLimit_NamesTheSearchThatReachesTheRest()
    {
        var container = CreateContainerWithManyMembers();

        var answer = await _tools.DescribeElementAsync(container.Id);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("more not shown"));
            Assert.That(answer, Does.Contain("search_elements"));
            Assert.That(answer, Does.Contain(container.FullName.ToLowerInvariant()));
        });
    }

    /// <summary>
    ///     Advice a caller cannot act on is worse than none: it spends a call and comes back empty. So
    ///     the term the notice hands out is run here, and it has to find a member that the truncated
    ///     list left out.
    /// </summary>
    [Test]
    public async Task Describe_TheSearchItSuggests_FindsTheChildrenItCouldNotShow()
    {
        var container = CreateContainerWithManyMembers();
        var truncated = await _tools.DescribeElementAsync(container.Id);
        var omitted = container.Children
            .First(child => !truncated.Contains($"id={child.Id}", StringComparison.Ordinal));

        var answer = await _tools.SearchElementsAsync(container.FullName.ToLowerInvariant(), 500);

        Assert.That(answer, Does.Contain($"id={omitted.Id}"));
    }

    /// <summary>
    ///     What the tool description promises about a path term: the full name is the whole path, so a
    ///     lowercase prefix lists what a container holds. Everything else in the graph stays out.
    /// </summary>
    [Test]
    public async Task Search_PathPrefix_ListsTheContentsOfThatContainerOnly()
    {
        var container = CreateContainerWithManyMembers();
        var elsewhere = _graph.CreateMethod("Unrelated", _orderService,
            fullName: "Sample.Core.Services.OrderService.Unrelated");

        var answer = await _tools.SearchElementsAsync("sample.core.services.billing type:method", 500);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain($"id={container.Children.First().Id}"));
            Assert.That(answer, Does.Not.Contain($"id={elsewhere.Id}"));
        });
    }

    /// <summary>
    ///     A container with more members than <c>describe_element</c> lists, named the way the parser
    ///     names things: the full name is the path down from the assembly, which is what makes a prefix
    ///     search over it work at all.
    /// </summary>
    private CodeElement CreateContainerWithManyMembers()
    {
        const string path = "Sample.Core.Services.Billing";
        var container = _graph.CreateClass("Billing", _namespace, path);

        for (var i = 0; i < 45; i++)
        {
            _graph.CreateMethod($"Step{i.ToString(CultureInfo.InvariantCulture)}", container,
                fullName: $"{path}.Step{i.ToString(CultureInfo.InvariantCulture)}");
        }

        return container;
    }

    [Test]
    public async Task Describe_ReportsAKnownAccessLevel()
    {
        var answer = await _tools.DescribeElementAsync(_orderService.Id);

        Assert.That(answer, Does.Contain("Access: Public"));
    }

    /// <summary>
    ///     Unknown means "nobody told us", not a value. Printing it would invite exactly the conclusion
    ///     the domain model warns against - reading it as public, or as private.
    /// </summary>
    [Test]
    public async Task Describe_OmitsAnUnknownAccessLevel()
    {
        var answer = await _tools.DescribeElementAsync(_place.Id);

        Assert.That(answer, Does.Not.Contain("Access:"));
    }

    /// <summary>
    ///     An external element has no analyzed dependencies, so "no outgoing relationships" would read
    ///     as a finding when it is really an absence of data.
    /// </summary>
    [Test]
    public async Task Describe_ExternalElement_SaysItsDependenciesWereNeverAnalyzed()
    {
        var external = _graph.CreateExternalClass("JsonSerializer", _namespace);

        var answer = await _tools.DescribeElementAsync(external.Id);

        Assert.That(answer, Does.Contain("External"));
        Assert.That(answer, Does.Contain("not analyzed"));
    }

    [Test]
    public async Task Describe_CountsRelationshipsInBothDirections()
    {
        _place.Relationships.Add(new Relationship(_place.Id, _validate.Id, RelationshipType.Calls));

        var outgoing = await _tools.DescribeElementAsync(_place.Id);
        var incoming = await _tools.DescribeElementAsync(_validate.Id);

        Assert.Multiple(() =>
        {
            Assert.That(outgoing, Does.Contain("Outgoing relationships (1): 1 Calls"));
            Assert.That(incoming, Does.Contain("Incoming relationships: 1"));
        });
    }
}
