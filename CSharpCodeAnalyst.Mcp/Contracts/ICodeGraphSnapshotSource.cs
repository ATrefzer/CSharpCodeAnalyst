namespace CSharpCodeAnalyst.Mcp.Contracts;

/// <summary>
///     Supplies the MCP tools with the code graph to answer questions about. Implemented by the host
///     application, which owns the live graph. The live graph may be mutated during refactoring simulations.
///     The snapshot for the mcp server is stable.
/// </summary>
public interface ICodeGraphSnapshotSource
{
    /// <summary>
    ///     The current snapshot, taken fresh if the graph changed since the last call.
    ///     Returns <c>null</c> when no project is loaded - the normal state of a freshly started
    ///     application, not an error. Tools must report that as an answer rather than throwing, so a
    ///     caller learns what to do instead of seeing a protocol failure.
    /// </summary>
    Task<GraphSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
