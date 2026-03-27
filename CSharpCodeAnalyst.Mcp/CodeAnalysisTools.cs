using System.ComponentModel;
using System.Text;
using CodeGraph.Algorithms.Cycles;
using CodeGraph.Graph;
using ModelContextProtocol.Server;

namespace CSharpCodeAnalyst.Mcp;

[McpServerToolType]
public class CodeAnalysisTools(GraphService graphService)
{
    [McpServerTool, Description("Load a CodeGraph file (.cg) exported from CSharpCodeAnalyst. Must be called before using any analysis tools.")]
    public string load_graph(
        [Description("Absolute path to the .cg graph file exported from CSharpCodeAnalyst")] string file_path)
    {
        try
        {
            graphService.Load(file_path);
            var graph = graphService.Graph;
            var nodeCount = graph.Nodes.Count;
            var relCount = graph.GetAllRelationships().Count();
            return $"Graph loaded successfully: {nodeCount} elements, {relCount} relationships. File: {file_path}";
        }
        catch (Exception ex)
        {
            return $"Error loading graph: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get a summary of the loaded code graph: element counts by type, relationship counts, and top-level structure.")]
    public string get_graph_summary()
    {
        if (!graphService.IsLoaded)
            return "No graph loaded. Call load_graph first.";

        var graph = graphService.Graph;
        var sb = new StringBuilder();

        sb.AppendLine($"Graph file: {graphService.LoadedFilePath}");
        sb.AppendLine($"Total elements: {graph.Nodes.Count}");
        sb.AppendLine();

        // Count by type
        var byType = graph.Nodes.Values
            .GroupBy(n => n.ElementType)
            .OrderBy(g => g.Key.ToString())
            .Select(g => $"  {g.Key}: {g.Count()}");

        sb.AppendLine("Elements by type:");
        foreach (var line in byType)
            sb.AppendLine(line);

        sb.AppendLine();
        sb.AppendLine($"Total relationships: {graph.GetAllRelationships().Count()}");

        // Top-level namespaces
        var namespaces = graph.Nodes.Values
            .Where(n => n.ElementType == CodeElementType.Namespace && n.Parent?.ElementType == CodeElementType.Assembly)
            .OrderBy(n => n.FullName)
            .Take(20)
            .Select(n => $"  {n.FullName}");

        sb.AppendLine();
        sb.AppendLine("Top-level namespaces:");
        foreach (var ns in namespaces)
            sb.AppendLine(ns);

        return sb.ToString();
    }

    [McpServerTool, Description("Find all dependency cycles in the code graph. Returns strongly connected components (groups of elements that mutually depend on each other). Specify the granularity level to control which element types are analyzed.")]
    public string get_cycles(
        [Description("Granularity level: 'Namespace', 'Class', or 'Method'. Namespace finds cycles between namespaces, Class between classes/types, Method between individual methods.")] string level = "Namespace")
    {
        if (!graphService.IsLoaded)
            return "No graph loaded. Call load_graph first.";

        if (!Enum.TryParse<CodeElementType>(level, true, out var elementType))
            return $"Unknown level '{level}'. Use: Namespace, Class, or Method.";

        var graph = graphService.Graph;

        // Build a filtered graph at the requested level
        var filteredGraph = BuildGraphAtLevel(graph, elementType);
        var cycleGroups = CycleFinder.FindCycleGroups(filteredGraph);

        if (cycleGroups.Count == 0)
            return $"No cycles found at {level} level.";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {cycleGroups.Count} cycle(s) at {level} level:");
        sb.AppendLine();

        for (var i = 0; i < cycleGroups.Count; i++)
        {
            var group = cycleGroups[i];
            var members = group.CodeGraph.Nodes.Values
                .Where(n => !n.IsExternal)
                .OrderBy(n => n.FullName)
                .Select(n => n.FullName)
                .ToList();

            sb.AppendLine($"Cycle {i + 1} ({members.Count} elements):");
            foreach (var member in members)
                sb.AppendLine($"  - {member}");

            // Show the dependencies within the cycle
            var deps = group.CodeGraph.GetAllRelationships()
                .Select(r => $"    {group.CodeGraph.Nodes[r.SourceId].FullName} --[{r.Type}]--> {group.CodeGraph.Nodes[r.TargetId].FullName}")
                .OrderBy(s => s)
                .Distinct();

            sb.AppendLine("  Dependencies within cycle:");
            foreach (var dep in deps)
                sb.AppendLine(dep);

            sb.AppendLine();
        }

        return sb.ToString();
    }

    [McpServerTool, Description("Get all incoming and outgoing dependencies of a specific code element (namespace, class, method, etc.). Use the full name as shown in get_graph_summary.")]
    public string get_dependencies(
        [Description("Full name of the code element, e.g. 'MyApp.Services' or 'MyApp.Services.OrderService'")] string element_name)
    {
        if (!graphService.IsLoaded)
            return "No graph loaded. Call load_graph first.";

        var graph = graphService.Graph;

        var element = graph.Nodes.Values.FirstOrDefault(n =>
            n.FullName.Equals(element_name, StringComparison.OrdinalIgnoreCase));

        if (element is null)
            return $"Element '{element_name}' not found. Use get_graph_summary to see available namespaces.";

        var sb = new StringBuilder();
        sb.AppendLine($"Dependencies for: {element.FullName} ({element.ElementType})");
        sb.AppendLine();

        // Outgoing
        var outgoing = element.Relationships
            .Where(r => graph.Nodes.ContainsKey(r.TargetId))
            .Select(r => (graph.Nodes[r.TargetId], r.Type))
            .Where(t => !t.Item1.IsExternal)
            .OrderBy(t => t.Item1.FullName)
            .ToList();

        sb.AppendLine($"Outgoing ({outgoing.Count}):");
        foreach (var (target, relType) in outgoing)
            sb.AppendLine($"  --[{relType}]--> {target.FullName}");

        sb.AppendLine();

        // Incoming
        var incoming = graph.GetAllRelationships()
            .Where(r => r.TargetId == element.Id && graph.Nodes.ContainsKey(r.SourceId))
            .Select(r => (graph.Nodes[r.SourceId], r.Type))
            .Where(t => !t.Item1.IsExternal)
            .OrderBy(t => t.Item1.FullName)
            .ToList();

        sb.AppendLine($"Incoming ({incoming.Count}):");
        foreach (var (source, relType) in incoming)
            sb.AppendLine($"  {source.FullName} --[{relType}]-->");

        return sb.ToString();
    }

    /// <summary>
    /// Returns a projected graph containing only elements at or above the requested level.
    /// This allows cycle detection at namespace, class, or method granularity.
    /// </summary>
    private static CodeGraph.Graph.CodeGraph BuildGraphAtLevel(CodeGraph.Graph.CodeGraph original, CodeElementType targetLevel)
    {
        var levelTypes = GetTypesAtOrAboveLevel(targetLevel);

        var filtered = new CodeGraph.Graph.CodeGraph();

        // Add nodes at the target level
        foreach (var node in original.Nodes.Values.Where(n => levelTypes.Contains(n.ElementType)))
        {
            filtered.Nodes[node.Id] = node;
        }

        return filtered;
    }

    private static HashSet<CodeElementType> GetTypesAtOrAboveLevel(CodeElementType level)
    {
        return level switch
        {
            CodeElementType.Namespace => [CodeElementType.Assembly, CodeElementType.Namespace],
            CodeElementType.Class => [CodeElementType.Assembly, CodeElementType.Namespace,
                CodeElementType.Class, CodeElementType.Interface, CodeElementType.Struct,
                CodeElementType.Enum, CodeElementType.Delegate, CodeElementType.Record],
            CodeElementType.Method => [CodeElementType.Assembly, CodeElementType.Namespace,
                CodeElementType.Class, CodeElementType.Interface, CodeElementType.Struct,
                CodeElementType.Enum, CodeElementType.Delegate, CodeElementType.Record,
                CodeElementType.Method, CodeElementType.Property, CodeElementType.Field,
                CodeElementType.Event],
            _ => [.. Enum.GetValues<CodeElementType>()]
        };
    }
}
