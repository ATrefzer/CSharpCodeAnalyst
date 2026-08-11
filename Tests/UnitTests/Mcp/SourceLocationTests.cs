using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Mcp.Contracts;
using CSharpCodeAnalyst.Mcp.Tools;

namespace CodeParserTests.UnitTests.Mcp;

/// <summary>
///     Pins how the tools report where code sits.
///     <para>
///         The rule they follow: keep the directory, drop the prefix every file in the graph shares.
///         The bare file name would be shorter, but it does not name a file - this repository alone has
///         seven <c>Analyzer.cs</c> and four <c>Strings.Designer.cs</c>, because the convention puts the
///         meaning into the folder. A caller that cannot turn a location back into a path cannot open
///         the code, which is the whole point of reporting one.
///     </para>
/// </summary>
[TestFixture]
public class SourceLocationTests
{
    [SetUp]
    public void SetUp()
    {
        _graph = new TestCodeGraph();
        _assembly = _graph.CreateAssembly("Sample.Core");
    }

    private TestCodeGraph _graph = null!;
    private CodeElement _assembly = null!;

    private CodeElement ClassAt(string name, string file, int line = 1)
    {
        var element = _graph.CreateClass(name, _assembly);
        element.SourceLocations.Add(new SourceLocation(file, line, 1));
        return element;
    }

    /// <summary>
    ///     The case the whole thing exists for. Both files are called <c>Analyzer.cs</c>; only the
    ///     directory tells them apart, and what survives the shortening is exactly that directory.
    /// </summary>
    [Test]
    public async Task Search_SameFileNameInTwoDirectories_KeepsThemApart()
    {
        ClassAt("DeadCodeAnalyzer", @"D:\repo\Analyzers\DeadCode\Analyzer.cs", 12);
        ClassAt("MetricsAnalyzer", @"D:\repo\Analyzers\Metrics\Analyzer.cs", 30);

        var tools = new ElementTools(FakeSnapshotSource.With(_graph));
        var answer = await tools.SearchElementsAsync("analyzer");

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain(@"DeadCode\Analyzer.cs:12"));
            Assert.That(answer, Does.Contain(@"Metrics\Analyzer.cs:30"));
        });
    }

    /// <summary>
    ///     The prefix is the part that is identical on every line of an answer, so it is the part worth
    ///     dropping - and the only part. It is the deepest shared directory, not the repository root:
    ///     anything above it is as uninformative as the drive letter.
    /// </summary>
    [Test]
    public async Task Search_CommonRoot_IsStrippedFromTheLocation()
    {
        ClassAt("OrderService", @"D:\repo\Core\Orders\OrderService.cs", 7);
        ClassAt("Invoice", @"D:\repo\Core\Billing\Invoice.cs");

        var tools = new ElementTools(FakeSnapshotSource.With(_graph));
        var answer = await tools.SearchElementsAsync("orderservice");

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain(@"Orders\OrderService.cs:7"));
            Assert.That(answer, Does.Not.Contain(@"D:\repo"));
        });
    }

    /// <summary>
    ///     Without a shared root there is no redundancy to remove, and a shortened path would only lose
    ///     information. The full path is long but never wrong.
    /// </summary>
    [Test]
    public async Task Search_FilesWithoutACommonRoot_KeepTheFullPath()
    {
        ClassAt("OrderService", @"D:\one\OrderService.cs", 7);
        ClassAt("Invoice", @"E:\two\Invoice.cs");

        var tools = new ElementTools(FakeSnapshotSource.With(_graph));
        var answer = await tools.SearchElementsAsync("orderservice");

        Assert.That(answer, Does.Contain(@"D:\one\OrderService.cs:7"));
    }

    /// <summary>
    ///     A relative path is worthless without the directory it is relative to: the caller's working
    ///     directory need not be the one the graph was parsed in, and need not even be the same machine.
    /// </summary>
    [Test]
    public async Task GraphInfo_ReportsTheRootTheLocationsAreRelativeTo()
    {
        ClassAt("OrderService", @"D:\repo\Core\Orders\OrderService.cs");
        ClassAt("Invoice", @"D:\repo\Core\Billing\Invoice.cs");

        var tools = new GraphInfoTools(FakeSnapshotSource.With(_graph));
        var answer = await tools.GraphInfoAsync();

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain(@"Source root: D:\repo\Core"));
            Assert.That(answer, Does.Contain("relative to it"));
        });
    }

    [Test]
    public void SourceRoot_GraphWithoutLocations_IsNull()
    {
        _graph.CreateClass("OrderService", _assembly);

        var snapshot = new GraphSnapshot(_graph);

        Assert.That(snapshot.SourceRoot, Is.Null);
    }

    /// <summary>
    ///     Several importers report a bare file name. It says nothing about a common root, and letting
    ///     it suppress the prefix for everyone else would be the wrong trade.
    /// </summary>
    [Test]
    public void SourceRoot_FileWithoutADirectory_DoesNotDefeatTheRoot()
    {
        ClassAt("OrderService", @"D:\repo\Core\OrderService.cs");
        ClassAt("Generated", "Generated.cs");

        var snapshot = new GraphSnapshot(_graph);

        Assert.That(snapshot.SourceRoot, Is.EqualTo(@"D:\repo\Core"));
    }

    /// <summary>
    ///     Imported graphs use forward slashes. The root has to be built with the separator the data
    ///     actually uses, or it no longer prefixes the paths it was derived from and every location
    ///     falls back to the full path.
    /// </summary>
    [Test]
    public async Task Search_ForwardSlashPaths_AreShortenedToo()
    {
        ClassAt("OrderService", "/home/dev/repo/core/order_service.dart", 7);
        ClassAt("Invoice", "/home/dev/repo/billing/invoice.dart");

        var tools = new ElementTools(FakeSnapshotSource.With(_graph));
        var answer = await tools.SearchElementsAsync("orderservice");

        Assert.That(answer, Does.Contain("core/order_service.dart:7"));
        Assert.That(answer, Does.Not.Contain("/home/dev"));
    }

    /// <summary>
    ///     describe_element lists every declaration, which is where a partial class becomes visible.
    ///     Those are full paths in the graph and get the same treatment.
    /// </summary>
    [Test]
    public async Task Describe_ListsDeclarationsRelativeToTheRoot()
    {
        var element = ClassAt("OrderService", @"D:\repo\Core\Orders\OrderService.cs", 7);
        element.SourceLocations.Add(new SourceLocation(@"D:\repo\Core\Orders\OrderService.Api.cs", 3, 1));
        ClassAt("Invoice", @"D:\repo\Core\Billing\Invoice.cs");

        var tools = new ElementTools(FakeSnapshotSource.With(_graph));
        var answer = await tools.DescribeElementAsync(element.Id);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("Declarations:"));
            Assert.That(answer, Does.Contain(@"Orders\OrderService.cs:7"));
            Assert.That(answer, Does.Contain(@"Orders\OrderService.Api.cs:3"));
            Assert.That(answer, Does.Not.Contain(@"D:\repo"));
        });
    }

    /// <summary>
    ///     The call site on a relationship is a location like any other, and was shortened to the bare
    ///     file name in the same way.
    /// </summary>
    [Test]
    public async Task Relationships_CallSite_IsRelativeToTheRoot()
    {
        var caller = ClassAt("OrderService", @"D:\repo\Core\Orders\OrderService.cs", 7);
        var callee = ClassAt("Validator", @"D:\repo\Core\Billing\Validator.cs");

        var relationship = new Relationship(caller.Id, callee.Id, RelationshipType.Uses);
        relationship.SourceLocations.Add(
            new SourceLocation(@"D:\repo\Core\Orders\OrderService.cs", 42, 1));
        caller.Relationships.Add(relationship);

        var tools = new RelationshipTools(FakeSnapshotSource.With(_graph));
        var answer = await tools.FindOutgoingRelationshipsAsync(caller.Id);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain(@"at Orders\OrderService.cs:42"));
            Assert.That(answer, Does.Not.Contain(@"D:\repo"));
        });
    }
}
