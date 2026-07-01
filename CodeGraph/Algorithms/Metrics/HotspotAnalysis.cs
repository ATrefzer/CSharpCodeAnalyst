using CodeGraph.Graph;

namespace CodeGraph.Algorithms.Metrics;

/// <summary>
///     One row of the hotspot result: a type with its coupling degrees and its
///     transitive importance (PageRank) in the type-level dependency graph.
/// </summary>
public class TypeHotspot(CodeElement type)
{
    public CodeElement Type { get; } = type;

    /// <summary>How many other types depend on this one (deduplicated incoming type edges).</summary>
    public int FanIn { get; set; }

    /// <summary>How many other types this one depends on (deduplicated outgoing type edges).</summary>
    public int FanOut { get; set; }

    /// <summary>Raw PageRank. The values over all types sum to 1.</summary>
    public double PageRank { get; set; }

    /// <summary>
    ///     PageRank normalized so the average type scores 1.0 (PageRank * typeCount).
    ///     A score of 5.0 means "five times more central than the average type".
    /// </summary>
    public double Score { get; set; }

    /// <summary>1-based position when sorted by PageRank, descending.</summary>
    public int Rank { get; set; }
}

/// <summary>
///     Ranks the types of a code graph by how central they are in the dependency structure,
///     to answer "which types should I understand first?" on an unfamiliar codebase.
///
///     The analysis lifts the fine-grained relationships (method calls, field uses, ...) to
///     the containing types and works on the resulting type-level graph:
///     - Fan-in / fan-out are the deduplicated in/out degrees. Ten calls from type A into type
///       B and a single call both count as one A->B edge; the strength of a coupling is not
///       modeled in this first version.
///     - PageRank measures transitive importance: a type is important when important types
///       depend on it, not merely when many types do. Edges are NOT reversed - importance
///       flows to the depended-upon types, which is exactly what we want to surface.
/// </summary>
public static class HotspotAnalysis
{
    private const double DampingFactor = 0.85;
    private const int MaxIterations = 100;
    private const double ConvergenceThreshold = 1e-6;

    /// <summary>
    ///     External types are excluded from both the result and the edges: hotspots are about
    ///     understanding the analyzed code, and ubiquitous framework types (object, string, ...)
    ///     would otherwise dominate the fan-in ranking.
    /// </summary>
    public static List<TypeHotspot> Calculate(Graph.CodeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var hotspots = graph.Nodes.Values
            .Where(n => IsType(n) && !n.IsExternal)
            .ToDictionary(n => n.Id, n => new TypeHotspot(n));

        if (hotspots.Count == 0)
        {
            return [];
        }

        // Lift every relationship to the (source type, target type) level and deduplicate.
        // Self edges (a type depending on itself) carry no coupling signal and are dropped.
        var typeEdges = new HashSet<(string Source, string Target)>();
        foreach (var relationship in graph.GetAllRelationships())
        {
            if (!IsDependency(relationship.Type))
            {
                continue;
            }

            var source = ContainingType(graph, relationship.SourceId);
            var target = ContainingType(graph, relationship.TargetId);

            if (source is null || target is null || source.Id == target.Id)
            {
                continue;
            }

            if (!hotspots.ContainsKey(source.Id) || !hotspots.ContainsKey(target.Id))
            {
                continue; // Endpoint is external or otherwise not a ranked type.
            }

            typeEdges.Add((source.Id, target.Id));
        }

        // Deduplicated degrees and the outgoing adjacency used by PageRank.
        var outgoing = hotspots.Keys.ToDictionary(id => id, _ => new List<string>());
        foreach (var (source, target) in typeEdges)
        {
            hotspots[source].FanOut += 1;
            hotspots[target].FanIn += 1;
            outgoing[source].Add(target);
        }

        var pageRank = CalculatePageRank(hotspots.Keys.ToList(), outgoing);

        var count = hotspots.Count;
        foreach (var (id, rank) in pageRank)
        {
            hotspots[id].PageRank = rank;
            hotspots[id].Score = rank * count;
        }

        var result = hotspots.Values
            .OrderByDescending(h => h.PageRank)
            .ThenBy(h => h.Type.FullName)
            .ToList();

        for (var i = 0; i < result.Count; i++)
        {
            result[i].Rank = i + 1;
        }

        return result;
    }

    /// <summary>
    ///     Standard PageRank via power iteration. Dangling types (no outgoing edges) would leak
    ///     rank out of the system, so their rank is redistributed uniformly across all types.
    /// </summary>
    private static Dictionary<string, double> CalculatePageRank(
        List<string> nodes,
        Dictionary<string, List<string>> outgoing)
    {
        var count = nodes.Count;
        var rank = nodes.ToDictionary(id => id, _ => 1.0 / count);

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var danglingRank = nodes.Where(id => outgoing[id].Count == 0).Sum(id => rank[id]);

            var next = nodes.ToDictionary(
                id => id,
                _ => (1.0 - DampingFactor) / count + DampingFactor * danglingRank / count);

            foreach (var source in nodes)
            {
                var targets = outgoing[source];
                if (targets.Count == 0)
                {
                    continue;
                }

                var share = DampingFactor * rank[source] / targets.Count;
                foreach (var target in targets)
                {
                    next[target] += share;
                }
            }

            var delta = nodes.Sum(id => Math.Abs(next[id] - rank[id]));
            rank = next;

            if (delta < ConvergenceThreshold)
            {
                break;
            }
        }

        return rank;
    }

    /// <summary>
    ///     Returns the type that contains the given element (a type element maps to itself),
    ///     or null if the element sits above the type level (namespace, assembly).
    /// </summary>
    private static CodeElement? ContainingType(Graph.CodeGraph graph, string elementId)
    {
        var current = graph.TryGetCodeElement(elementId);
        while (current is not null && !IsType(current))
        {
            current = current.Parent;
        }

        return current;
    }

    /// <summary>
    ///     Whether a relationship expresses a compile-time dependency (source depends on target),
    ///     which is what PageRank and the degrees are built on. Excluded:
    ///     - Containment: the parent/child hierarchy, not a dependency.
    ///     - Bundled: artificial edges the UI creates to fold several relationships together.
    ///     - Handles: an event-handler registration. The model stores it as handler -> event, but
    ///       it is the callback wiring (the event later calls the handler), not a dependency of
    ///       the handler on the event. Counting it would give handlers spurious importance.
    /// </summary>
    private static bool IsDependency(RelationshipType type)
    {
        return type is not (RelationshipType.Containment or RelationshipType.Bundled
            or RelationshipType.Handles);
    }

    private static bool IsType(CodeElement element)
    {
        return element.ElementType is CodeElementType.Class or CodeElementType.Interface
            or CodeElementType.Struct or CodeElementType.Record or CodeElementType.Enum
            or CodeElementType.Delegate;
    }
}
