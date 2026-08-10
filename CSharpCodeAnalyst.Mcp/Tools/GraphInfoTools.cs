using System.ComponentModel;
using System.Globalization;
using System.Text;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Mcp.Contracts;
using ModelContextProtocol.Server;

namespace CSharpCodeAnalyst.Mcp.Tools;

/// <summary>
///     Tells a caller what it is actually looking at before it starts asking questions about it.
/// </summary>
[McpServerToolType]
public sealed class GraphInfoTools(ICodeGraphSnapshotSource snapshotSource)
{
    [McpServerTool(Name = "graph_info", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Describes the code graph currently loaded in CSharp Code Analyst: what it was built from, " +
        "when it was captured, how large it is, and which assemblies it contains. Call this first. " +
        "The graph is a snapshot, so it can be older than the files on disk, and it can contain " +
        "simulated refactorings that were never applied to the source - both are reported here and " +
        "both change how much the other answers can be trusted.")]
    public async Task<string> GraphInfoAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshotSource.GetSnapshotAsync(cancellationToken);
        if (snapshot is null)
        {
            return ToolText.NoProjectLoaded;
        }

        var graph = snapshot.Graph;
        var text = new StringBuilder();

        text.Append("Source: ").AppendLine(string.IsNullOrEmpty(snapshot.SourceName)
            ? "unknown"
            : snapshot.SourceName);
        text.Append("Captured: ")
            .Append(snapshot.CapturedAtUtc.ToString("u", CultureInfo.InvariantCulture))
            .AppendLine(" (source files may have changed since)");

        if (snapshot.ContainsRefactorings)
        {
            text.AppendLine(
                "WARNING: this graph contains simulated refactorings. It describes a hypothetical " +
                "code base, not the code on disk. Say so when reporting anything derived from it.");
        }

        text.Append("Code elements: ")
            .AppendLine(graph.Nodes.Count.ToString(CultureInfo.InvariantCulture));
        text.Append("Relationships: ")
            .AppendLine(graph.GetAllRelationships().Count().ToString(CultureInfo.InvariantCulture));

        AppendAssemblies(text, graph);

        text.AppendLine();
        text.AppendLine(
            "Element ids are opaque and valid only while this server runs. Use search_elements to " +
            "find an element and obtain its id.");

        return text.ToString();
    }

    private static void AppendAssemblies(StringBuilder text, CodeGraph.Graph.CodeGraph graph)
    {
        // Roots are the assemblies: the parser puts everything below one, inserting a synthetic
        // namespace where code sits at the root, so nothing else ends up parentless.
        var assemblies = graph.GetRoots()
            .Where(root => root.ElementType == CodeElementType.Assembly)
            .OrderBy(root => root.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (assemblies.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.Append("Assemblies (").Append(assemblies.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine("):");
        foreach (var assembly in assemblies)
        {
            text.Append("  ").Append(assembly.Name);
            if (assembly.IsExternal)
            {
                text.Append("  [external]");
            }

            text.AppendLine();
        }
    }
}
