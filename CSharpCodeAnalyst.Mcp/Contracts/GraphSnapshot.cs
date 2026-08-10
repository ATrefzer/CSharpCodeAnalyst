namespace CSharpCodeAnalyst.Mcp.Contracts;

/// <summary>
///     A consistent, read only view of the code graph the application had loaded at
///     <see cref="CapturedAtUtc" />. Never the live graph: the application mutates that one in place
///     during a refactoring simulation, and a query walking it at the same time would see a half
///     changed structure. The snapshot belongs to the MCP layer alone, so nothing can change under a
///     running query and no locking is needed anywhere.
///     <para>
///         Everything except <see cref="Graph" /> exists so a caller can judge how much to trust the
///         answer: whether the graph is still current, and whether it describes code that actually
///         exists.
///     </para>
/// </summary>
/// <param name="Graph">The copied graph. Treat as immutable.</param>
/// <param name="SourceName">
///     What the graph was built from - a solution or project file name. Empty when unknown, which is
///     the case for a graph produced by an importer that does not report one.
/// </param>
/// <param name="CapturedAtUtc">When the copy was taken. Source files may have changed since.</param>
/// <param name="ContainsRefactorings">
///     Whether the user simulated refactorings after loading. If true the graph describes a hypothetical
///     code base, not the one on disk - a fact any consumer has to be told, because the difference is
///     invisible in the data itself.
/// </param>
public sealed record GraphSnapshot(
    CodeGraph.Graph.CodeGraph Graph,
    string SourceName,
    DateTimeOffset CapturedAtUtc,
    bool ContainsRefactorings);
