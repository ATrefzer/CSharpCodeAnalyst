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

        var tools = new GraphInfoTools(FakeSnapshotSource.With(graph));
        var answer = await tools.GraphInfoAsync();

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("Code elements: 4"));
            Assert.That(answer, Does.Contain("Relationships: 1"));
            Assert.That(answer, Does.Contain("Sample.Core"));
        });
    }

    /// <summary>
    ///     The kinds present are what makes a 'type:' filter worth trying. An imported graph has fewer
    ///     of them than the model defines, and the difference is not guessable from the outside.
    /// </summary>
    [Test]
    public async Task GraphInfo_ReportsWhichKindsOfElementExist()
    {
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("Sample.Core");
        var type = graph.CreateClass("OrderService", assembly);
        graph.CreateMethod("Place", type);
        graph.CreateMethod("Cancel", type);

        var tools = new GraphInfoTools(FakeSnapshotSource.With(graph));
        var answer = await tools.GraphInfoAsync();

        Assert.Multiple(() =>
        {
            // Ordered by count, so the shape of the code base is readable at a glance.
            Assert.That(answer, Does.Contain("Kinds: 2 Method, 1 Assembly, 1 Class"));
            Assert.That(answer, Does.Contain("type:"));

            // A kind the graph does not hold must not be listed - it would only invite a search that
            // cannot match.
            Assert.That(answer, Does.Not.Contain("Interface"));
        });
    }

    /// <summary>
    ///     The graph calls its roots assemblies whatever produced them. For a Dart or Python import the
    ///     word is wrong, and a caller taking it literally looks for something that does not exist.
    /// </summary>
    [Test]
    public async Task GraphInfo_SaysWhatAnAssemblyIsForAnImportedGraph()
    {
        var graph = new TestCodeGraph();
        graph.CreateAssembly("sample_app");

        var tools = new GraphInfoTools(FakeSnapshotSource.With(graph));
        var answer = await tools.GraphInfoAsync();

        Assert.That(answer, Does.Contain("package").And.Contain("module"));
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
    ///     The application, the server and usually the client side name all say "C#". The graph does
    ///     not, and this is the only place that says what is really loaded.
    /// </summary>
    [Test]
    public async Task GraphInfo_ReportsTheLanguageTheCodeIsWrittenIn()
    {
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("sample_app");
        var type = graph.CreateClass("OrderService", assembly);
        type.SourceLocations.Add(new SourceLocation(@"D:\app\lib\order_service.dart", 3, 1));
        var other = graph.CreateClass("Invoice", assembly);
        other.SourceLocations.Add(new SourceLocation(@"D:\app\lib\invoice.dart", 5, 1));

        var tools = new GraphInfoTools(FakeSnapshotSource.With(graph));
        var answer = await tools.GraphInfoAsync();

        Assert.That(answer, Does.Contain("Dart").And.Contain("2 files"));
        Assert.That(answer, Does.Not.Contain("C#"));
    }

    /// <summary>
    ///     Several elements per file is the normal case, so the count has to be of files, not of the
    ///     locations they were found through.
    /// </summary>
    [Test]
    public async Task GraphInfo_CountsFilesNotLocations()
    {
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("Sample.Core");
        var type = graph.CreateClass("OrderService", assembly);
        type.SourceLocations.Add(new SourceLocation(@"D:\repo\OrderService.cs", 3, 1));
        var method = graph.CreateMethod("Place", type);
        method.SourceLocations.Add(new SourceLocation(@"D:\repo\OrderService.cs", 9, 5));

        var tools = new GraphInfoTools(FakeSnapshotSource.With(graph));
        var answer = await tools.GraphInfoAsync();

        Assert.That(answer, Does.Contain("C# (1 file)"));
    }

    /// <summary>
    ///     A graph without locations - a plain text import, for instance. Saying nothing would leave
    ///     the language to be guessed from the assembly names.
    /// </summary>
    [Test]
    public async Task GraphInfo_WithoutFileLocations_SaysTheLanguageIsUnknown()
    {
        var graph = new TestCodeGraph();
        graph.CreateAssembly("Sample.Core");

        var tools = new GraphInfoTools(FakeSnapshotSource.With(graph));
        var answer = await tools.GraphInfoAsync();

        Assert.That(answer, Does.Contain("Languages: unknown"));
    }

    /// <summary>
    ///     The tool description promises a source root. When there is none, the locations in every
    ///     other answer are absolute - a caller that is told neither has to guess which it is holding.
    /// </summary>
    [Test]
    public async Task GraphInfo_WithoutACommonRoot_SaysSoRatherThanOmittingTheLine()
    {
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("sample_app");
        var mine = graph.CreateClass("OrderService", assembly);
        mine.SourceLocations.Add(new SourceLocation(@"D:\app\lib\order_service.dart", 3, 1));
        var package = graph.CreateClass("Widget", assembly);
        package.SourceLocations.Add(new SourceLocation(@"C:\pub-cache\flutter\widget.dart", 1, 1));

        var tools = new GraphInfoTools(FakeSnapshotSource.With(graph));
        var answer = await tools.GraphInfoAsync();

        Assert.That(answer, Does.Contain("Source root: none"));
        Assert.That(answer, Does.Contain("full paths"));
    }

    [Test]
    public async Task GraphInfo_WithACommonRoot_ReportsItAsThePrefixOfEveryLocation()
    {
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("Sample.Core");
        var type = graph.CreateClass("OrderService", assembly);
        type.SourceLocations.Add(new SourceLocation(@"D:\repo\Core\OrderService.cs", 3, 1));

        var tools = new GraphInfoTools(FakeSnapshotSource.With(graph));
        var answer = await tools.GraphInfoAsync();

        Assert.That(answer, Does.Contain(@"Source root: D:\repo\Core"));
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
