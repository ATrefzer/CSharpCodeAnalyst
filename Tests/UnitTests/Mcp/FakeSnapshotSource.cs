using CSharpCodeAnalyst.Mcp.Contracts;

namespace CodeParserTests.UnitTests.Mcp;

/// <summary>
///     Stands in for the application. The tools only ever see a snapshot, so a fake that hands one out
///     is enough to test them - no WPF, no server, no dispatcher.
/// </summary>
internal sealed class FakeSnapshotSource(GraphSnapshot? snapshot) : ICodeGraphSnapshotSource
{
    /// <summary>The state of a freshly started application: running, but nothing opened yet.</summary>
    public static FakeSnapshotSource Empty()
    {
        return new FakeSnapshotSource(null);
    }

    public static FakeSnapshotSource With(CSharpCodeAnalyst.CodeGraph.Graph.CodeGraph graph)
    {
        return new FakeSnapshotSource(new GraphSnapshot(graph));
    }

    public Task<GraphSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(snapshot);
    }
}
