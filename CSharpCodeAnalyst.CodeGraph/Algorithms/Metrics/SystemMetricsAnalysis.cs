using CSharpCodeAnalyst.CodeGraph.Algorithms.Cycles;
using CSharpCodeAnalyst.CodeGraph.Contracts;

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

    /// <summary>
    ///     Feedback density in [0,1]: the share of type dependencies that end up pointing backward against the best possible
    ///     layering of the type graph (an approximate minimum feedback arc set).
    ///     0 = the type graph is a cleanly layered DAG, 1 = every dependency fights the layering.
    ///     Finer-grained companion to <see cref="Cyclicity" />: cyclicity counts the entangled
    ///     <em>types</em>, feedback density counts the <em>edges</em> one would cut to break the cycles.
    /// </summary>
    public double FeedbackDensity { get; init; }
}

/// <summary>
///     Computes <see cref="SystemMetrics" /> on the shared type-level dependency graph
///     (<see cref="TypeGraph" />). The graph is built once and every metric stage reuses it, so the
///     lift-to-type and deduplication work is not repeated per metric.
/// </summary>
public static class SystemMetricsAnalysis
{
    public static SystemMetrics Calculate(Graph.CodeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var typeGraph = TypeGraph.Build(graph);
        var n = typeGraph.VertexCount;

        if (n < 2)
        {
            return new SystemMetrics { TypeCount = n, TypeDependencyCount = 0, PropagationCost = 0.0 };
        }

        return new SystemMetrics
        {
            TypeCount = n,
            TypeDependencyCount = typeGraph.EdgeCount,
            PropagationCost = CalculatePropagationCost(typeGraph),
            Cyclicity = CalculateCyclicity(typeGraph),
            FeedbackDensity = FeedbackArcAnalysis.Analyze(typeGraph).FeedbackDensity
        };
    }

    /// <summary>
    ///     For each type, count how many OTHER types it can transitively reach and average that over all
    ///     types. Mirrors TypeDependencyAnalysis.CalculateBlastRadius (same transitive closure, following
    ///     the outgoing edges instead of the incoming ones).
    /// </summary>
    private static double CalculatePropagationCost(TypeGraph graph)
    {
        var n = graph.VertexCount;

        long reachablePairs = 0;
        var reached = new HashSet<string>();
        var queue = new Queue<string>(); // Bfs
        foreach (var start in graph.Vertices)
        {
            reached.Clear();
            queue.Enqueue(start);

            // "start" is the entry point of the walk but not part of its own reach, so it is never
            // added to "reached" (guarded below to survive cycles).
            while (queue.Count > 0)
            {
                foreach (var next in graph.Out[queue.Dequeue()])
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

        return (double)reachablePairs / ((long)n * (n - 1));
    }

    /// <summary>
    ///     Share of types that belong to a cycle. We run the shared Tarjan SCC algorithm on the type
    ///     graph and count the types that sit in a strongly connected component of two or more types
    ///     (a single type is trivially its own SCC and does not count; self edges were already dropped).
    /// </summary>
    private static double CalculateCyclicity(TypeGraph graph)
    {
        var sccs = Tarjan.FindStronglyConnectedComponents(new AdjacencyGraph(graph));

        var typesInCycles = sccs
            .Where(scc => scc.Vertices.Count >= 2)
            .Sum(scc => scc.Vertices.Count);

        return (double)typesInCycles / graph.VertexCount;
    }

    /// <summary>
    ///     Minimal adapter that exposes the shared <see cref="TypeGraph" /> (vertices + outgoing
    ///     adjacency) to the shared <see cref="Tarjan" /> algorithm. Only <see cref="GetVertices" /> and
    ///     <see cref="GetNeighbors" /> are used by Tarjan; the rest satisfies the interface.
    /// </summary>
    private sealed class AdjacencyGraph(TypeGraph graph) : IGraphRepresentation<string>
    {
        public uint VertexCount => (uint)graph.VertexCount;

        public IReadOnlyCollection<string> GetVertices()
        {
            return graph.Vertices;
        }

        public IReadOnlyCollection<string> GetNeighbors(string vertex)
        {
            return graph.Out[vertex];
        }

        public bool IsVertex(string vertex)
        {
            return graph.Vertices.Contains(vertex);
        }

        public bool IsEdge(string source, string target)
        {
            return graph.Out.TryGetValue(source, out var neighbors) && neighbors.Contains(target);
        }
    }
}
