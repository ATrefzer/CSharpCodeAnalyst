using CSharpCodeAnalyst.CodeGraph.Algorithms.Cycles;
using CSharpCodeAnalyst.CodeGraph.Contracts;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;

/// <summary>
///     Metrics that describe the analyzed code base as a whole (one value for the whole system),
///     as opposed to the per-type / per-method metrics of the other analyzers.
/// </summary>
public class SystemMetrics
{
    /// <summary>Number of internal types the metrics are based on (the N of the analysis).</summary>
    public int TypeCount { get; init; }

    /// <summary>Distinct directed type-to-type dependencies (deduplicated, self edges dropped).</summary>
    public int TypeDependencyCount { get; init; }

    /// <summary>
    ///     Propagation cost in [0,1]: the share of ordered type pairs (A, B), A != B, where A can
    ///     transitively reach B. Intuitively the average fraction of the <em>other</em> types a change
    ///     to a random type can ripple to. 0 = fully decoupled, 1 = every type reaches every other.
    /// </summary>
    public double PropagationCost { get; init; }

    /// <summary>
    ///     Cyclicity in [0,1]: the share of types that sit inside a dependency cycle (a strongly
    ///     connected component of two or more types). 0 = fully acyclic, 1 = every type is entangled.
    /// </summary>
    public double Cyclicity { get; init; }
}

/// <summary>
///     Computes <see cref="SystemMetrics" /> on the type-level dependency graph. The type graph is
///     built exactly like <see cref="TypeDependencyAnalysis" />: relationships are lifted to their
///     containing type, deduplicated, self edges and external types are dropped.
/// </summary>
public static class SystemMetricsAnalysis
{
    public static SystemMetrics Calculate(Graph.CodeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var typeIds = graph.Nodes.Values
            .Where(n => n.IsType() && !n.IsExternal)
            .Select(n => n.Id)
            .ToHashSet();

        var n = typeIds.Count;
        var outgoing = typeIds.ToDictionary(id => id, _ => new List<string>());

        if (n < 2)
        {
            return new SystemMetrics { TypeCount = n, TypeDependencyCount = 0, PropagationCost = 0.0 };
        }

        var edges = new HashSet<(string Source, string Target)>();
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

            if (!typeIds.Contains(source.Id) || !typeIds.Contains(target.Id))
            {
                continue; // Endpoint is external or otherwise not a counted type.
            }

            if (edges.Add((source.Id, target.Id)))
            {
                outgoing[source.Id].Add(target.Id);
            }
        }

        // For each type, count how many OTHER types it can transitively reach. Mirrors
        // TypeDependencyAnalysis.CalculateBlastRadius (same transitive closure, following the
        // outgoing edges instead of the incoming ones).
        long reachablePairs = 0;
        var reached = new HashSet<string>();
        var queue = new Queue<string>(); // Bfs
        foreach (var start in typeIds)
        {
            reached.Clear();
            queue.Enqueue(start);

            // "start" is the entry point of the walk but not part of its own reach, so it is never
            // added to "reached" (guarded below to survive cycles).
            while (queue.Count > 0)
            {
                foreach (var next in outgoing[queue.Dequeue()])
                {
                    if (next == start || !reached.Add(next))
                    {
                        continue;
                    }

                    queue.Enqueue(next);
                }
            }

            reachablePairs += reached.Count;
        }

        var propagationCost = (double)reachablePairs / ((long)n * (n - 1));

        return new SystemMetrics
        {
            TypeCount = n,
            TypeDependencyCount = edges.Count,
            PropagationCost = propagationCost,
            Cyclicity = CalculateCyclicity(typeIds, outgoing, n)
        };
    }

    /// <summary>
    ///     Share of types that belong to a cycle. We run the shared Tarjan SCC algorithm on the type
    ///     graph and count the types that sit in a strongly connected component of two or more types
    ///     (a single type is trivially its own SCC and does not count; self edges were already dropped).
    /// </summary>
    private static double CalculateCyclicity(HashSet<string> typeIds, Dictionary<string, List<string>> outgoing, int n)
    {
        var graph = new AdjacencyGraph(typeIds, outgoing);
        var sccs = Tarjan.FindStronglyConnectedComponents(graph);

        var typesInCycles = sccs
            .Where(scc => scc.Vertices.Count >= 2)
            .Sum(scc => scc.Vertices.Count);

        return (double)typesInCycles / n;
    }

    private static CodeElement? ContainingType(Graph.CodeGraph graph, string elementId)
    {
        var current = graph.TryGetCodeElement(elementId);
        while (current is not null && !current.IsType())
        {
            current = current.Parent;
        }

        return current;
    }

    /// <summary>
    ///     Minimal adapter that exposes the already-built type graph (vertices + outgoing adjacency)
    ///     to the shared <see cref="Tarjan" /> algorithm. Only <see cref="GetVertices" /> and
    ///     <see cref="GetNeighbors" /> are used by Tarjan; the rest satisfies the interface.
    /// </summary>
    private sealed class AdjacencyGraph(HashSet<string> vertices, Dictionary<string, List<string>> outgoing)
        : IGraphRepresentation<string>
    {
        public uint VertexCount => (uint)vertices.Count;

        public IReadOnlyCollection<string> GetVertices()
        {
            return vertices;
        }

        public IReadOnlyCollection<string> GetNeighbors(string vertex)
        {
            return outgoing[vertex];
        }

        public bool IsVertex(string vertex)
        {
            return vertices.Contains(vertex);
        }

        public bool IsEdge(string source, string target)
        {
            return outgoing.TryGetValue(source, out var neighbors) && neighbors.Contains(target);
        }
    }
}
