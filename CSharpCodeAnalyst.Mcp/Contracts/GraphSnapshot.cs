namespace CSharpCodeAnalyst.Mcp.Contracts;

/// <summary>
///     A consistent, read only view of the code graph the application has loaded. Never the live
///     graph: the application mutates that one in place during a refactoring simulation, and a query
///     walking it at the same time would see a half changed structure. The snapshot belongs to the MCP
///     layer alone, so nothing can change under a running query and no locking is needed anywhere.
///     <para>
///         It carries no capture time. The only one this layer could record is when it took the copy -
///         lazily, on the first question asked after the graph was loaded - which says nothing about
///         how old the analysed code is and would read as if it did.
///     </para>
/// </summary>
public sealed record GraphSnapshot(CodeGraph.Graph.CodeGraph Graph)
{
    /// <summary>
    ///     The directory every source file in <see cref="Graph" /> sits under, and the prefix the tools
    ///     strip from the locations they report. Null when the graph has no common root, in which case
    ///     they report full paths. Derived from the graph rather than supplied, so every producer of a
    ///     snapshot gets it without knowing about it.
    /// </summary>
    public string? SourceRoot { get; } = SourcePaths.FindRoot(Graph);
}
