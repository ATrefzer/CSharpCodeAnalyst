using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Mcp.Tools;

namespace CodeParserTests.UnitTests.Mcp;

/// <summary>
///     Tests for <see cref="GraphInfoTools" />, the tool that tells a caller what it is looking at.
///     <para>
///         The assertions are on substrings, not on the exact layout: the wording is meant to stay
///         editable, while the facts it has to carry are not.
///     </para>
/// </summary>
[TestFixture]
public class GraphInfoToolsTests
{
    [Test]
    public async Task GraphInfo_WithoutAProject_SaysSoInsteadOfFailing()
    {
        var tools = new GraphInfoTools(FakeSnapshotSource.Empty());

        var answer = await tools.GraphInfoAsync();

        // Not an exception: a protocol error would tell the caller nothing about what to do next.
        Assert.That(answer, Does.Contain("No project is loaded"));
        Assert.That(answer, Does.Contain("open a solution"));
    }

    [Test]
    public async Task GraphInfo_ReportsCountsAndAssemblies()
    {
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("Sample.Core");
        var ns = graph.CreateNamespace("Services", assembly);
        var type = graph.CreateClass("OrderService", ns);
        var method = graph.CreateMethod("Place", type);
        method.Relationships.Add(new Relationship(method.Id, type.Id, RelationshipType.Uses));

        var tools = new GraphInfoTools(FakeSnapshotSource.With(graph, "sample.json"));
        var answer = await tools.GraphInfoAsync();

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("sample.json"));
            Assert.That(answer, Does.Contain("Code elements: 4"));
            Assert.That(answer, Does.Contain("Relationships: 1"));
            Assert.That(answer, Does.Contain("Sample.Core"));
        });
    }

    [Test]
    public async Task GraphInfo_MarksExternalAssemblies()
    {
        var graph = new TestCodeGraph();
        graph.CreateAssembly("Sample.Core");
        var external = graph.CreateAssembly("System.Text.Json");
        graph.Nodes[external.Id] = new CodeElement(external.Id, CodeElementType.Assembly,
            external.Name, external.FullName, null) { IsExternal = true };

        var tools = new GraphInfoTools(FakeSnapshotSource.With(graph));
        var answer = await tools.GraphInfoAsync();

        Assert.That(answer, Does.Contain("System.Text.Json").And.Contain("[external]"));
    }

    /// <summary>
    ///     The most important thing this tool does. A graph changed by the refactoring simulation
    ///     describes code that does not exist, and nothing in the data itself gives that away - so
    ///     every answer derived from it would silently be about a fiction.
    /// </summary>
    [Test]
    public async Task GraphInfo_WarnsWhenTheGraphContainsSimulatedRefactorings()
    {
        var graph = new TestCodeGraph();
        graph.CreateAssembly("Sample.Core");

        var tools = new GraphInfoTools(FakeSnapshotSource.With(graph, containsRefactorings: true));
        var answer = await tools.GraphInfoAsync();

        Assert.That(answer, Does.Contain("WARNING"));
        Assert.That(answer, Does.Contain("not the code on disk"));
    }

    [Test]
    public async Task GraphInfo_WithoutRefactorings_DoesNotWarn()
    {
        var graph = new TestCodeGraph();
        graph.CreateAssembly("Sample.Core");

        var tools = new GraphInfoTools(FakeSnapshotSource.With(graph));
        var answer = await tools.GraphInfoAsync();

        Assert.That(answer, Does.Not.Contain("WARNING"));
    }

    /// <summary>
    ///     Ids are regenerated on every parse. A caller that does not know this will try to reuse one
    ///     from an earlier session, so the entry point is named where it is first needed.
    /// </summary>
    [Test]
    public async Task GraphInfo_PointsAtSearchAsTheEntryPoint()
    {
        var graph = new TestCodeGraph();
        graph.CreateAssembly("Sample.Core");

        var tools = new GraphInfoTools(FakeSnapshotSource.With(graph));
        var answer = await tools.GraphInfoAsync();

        Assert.That(answer, Does.Contain("search_elements"));
    }
}
