using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;

/// <summary>
///     One row of the type-dependency result: a type with its coupling degrees, its change impact
///     (blast radius) and its transitive importance (PageRank) in the type-level dependency graph.
/// </summary>
public class TypeDependencyInfo(CodeElement type)
{
    public CodeElement Type { get; } = type;

    /// <summary>How many other types depend on this one (deduplicated incoming type edges).</summary>
    public int FanIn { get; set; }

    /// <summary>How many other types this one depends on (deduplicated outgoing type edges).</summary>
    public int FanOut { get; set; }

    /// <summary>
    ///     How many other types transitively depend on this one - the set of types that could be
    ///     affected if it changes. Unlike <see cref="Score" /> this is a flat count: every reachable
    ///     type counts as one, regardless of its own importance.
    /// </summary>
    public int BlastRadius { get; set; }

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
///     Describes how each type sits in the dependency structure, to help answer "which types should
///     I understand first?" and "how risky is it to change this type?" on an unfamiliar codebase.
///
///     The analysis lifts the fine-grained relationships (method calls, field uses, ...) to the
///     containing types and works on the resulting deduplicated type-level graph:
///     - Fan-in / fan-out are the in/out degrees. Ten calls from type A into type B and a single
///       call both count as one A->B edge; dependency is treated as a yes/no fact.
///     - Blast radius is the number of types that transitively depend on a type (change impact).
///     - PageRank measures transitive importance: a type is important when important types depend on
///       it, not merely when many types do. Edges are NOT reversed - importance flows to the
///       depended-upon types, which is exactly what we want to surface.
/// </summary>
public static class TypeDependencyAnalysis
{
    private const double DampingFactor = 0.85;
    private const int MaxIterations = 100;
    private const double ConvergenceThreshold = 1e-6;

    /// <summary>
    ///     External types are excluded from both the result and the edges: this is about
    ///     understanding the analyzed code, and ubiquitous framework types (object, string, ...)
    ///     would otherwise dominate the fan-in ranking.
    /// </summary>
    public static List<TypeDependencyInfo> Calculate(Graph.CodeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var results = graph.Nodes.Values
            .Where(n => n.IsType() && !n.IsExternal)
            .ToDictionary(n => n.Id, n => new TypeDependencyInfo(n));

        if (results.Count == 0)
        {
            return [];
        }

        // Lift every relationship to the (source type, target type) level and deduplicate.
        // Self edges (a type depending on itself) carry no coupling signal and are dropped.
        var typeEdges = new HashSet<(string Source, string Target)>();
        foreach (var relationship in graph.GetAllRelationships())
        {
            if (!relationship.Type.IsDependency())
            {
                continue;
            }

            var source = ContainingType(graph, relationship.SourceId);
            var target = ContainingType(graph, relationship.TargetId);

            if (source is null || target is null || source.Id == target.Id)
            {
                continue;
            }

            if (!results.ContainsKey(source.Id) || !results.ContainsKey(target.Id))
            {
                continue; // Endpoint is external or otherwise not a ranked type.
            }

            typeEdges.Add((source.Id, target.Id));
        }

        // Deduplicated degrees plus the outgoing / incoming adjacency used below.
        var outgoing = results.Keys.ToDictionary(id => id, _ => new List<string>());
        var incoming = results.Keys.ToDictionary(id => id, _ => new List<string>());
        foreach (var (source, target) in typeEdges)
        {
            results[source].FanOut += 1;
            results[target].FanIn += 1;
            outgoing[source].Add(target);
            incoming[target].Add(source);
        }

        var nodes = results.Keys.ToList();

        // Travels against the edges and counts affected types.
        var blastRadius = CalculateBlastRadius(nodes, incoming);
        foreach (var (id, radius) in blastRadius)
        {
            results[id].BlastRadius = radius;
        }

        // Travels along the edges and accumulates rank.
        var pageRank = CalculatePageRank(nodes, outgoing);
        var count = results.Count;
        foreach (var (id, rank) in pageRank)
        {
            results[id].PageRank = rank;
            results[id].Score = rank * count;
        }

        var ranked = results.Values
            .OrderByDescending(h => h.PageRank)
            .ThenBy(h => h.Type.FullName)
            .ToList();

        for (var i = 0; i < ranked.Count; i++)
        {
            ranked[i].Rank = i + 1;
        }

        return ranked;
    }

    /// <summary>
    ///     For each type, the number of other types that can transitively reach it by following
    ///     dependency edges - i.e. how many types could be affected if it changes. This is a plain
    ///     count of the incoming transitive closure; the type itself is never counted, even when it
    ///     sits in a dependency cycle. Cost is O(N * (N + E)); fine for on-demand analysis.
    /// </summary>
    private static Dictionary<string, int> CalculateBlastRadius(
        List<string> nodes,
        Dictionary<string, List<string>> incoming)
    {
        var result = new Dictionary<string, int>(nodes.Count);
        var visited = new HashSet<string>();
        var queue = new Queue<string>();

        foreach (var start in nodes)
        {
            visited.Clear();
            queue.Enqueue(start);

            // The start is the entry point of the walk but is not part of its own blast radius,
            // so it is never added to "visited" (guarded below to survive cycles).
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var dependent in incoming[current])
                {
                    if (dependent == start || !visited.Add(dependent))
                    {
                        continue;
                    }

                    queue.Enqueue(dependent);
                }
            }

            result[start] = visited.Count;
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
        while (current is not null && !current.IsType())
        {
            current = current.Parent;
        }

        return current;
    }
}
