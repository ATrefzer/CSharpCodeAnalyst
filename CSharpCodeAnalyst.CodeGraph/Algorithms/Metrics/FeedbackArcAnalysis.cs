namespace CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;

/// <summary>
///     Result of ordering a directed dependency graph so that as many edges as possible point in one
///     direction (a lower-triangular DSM / cleanly layered DAG). Whatever cannot be made to point
///     forward are the <see cref="FeedbackEdges" /> — the back-references that break the layering.
///     Arc = directed edge.
/// </summary>
public sealed class FeedbackArcResult
{
    public FeedbackArcResult(
        IReadOnlyList<string> order,
        int edgeCount,
        IReadOnlyCollection<(string Source, string Target)> feedbackEdges)
    {
        Order = order;
        EdgeCount = edgeCount;
        FeedbackEdges = feedbackEdges;
        FeedbackDensity = edgeCount == 0 ? 0.0 : (double)feedbackEdges.Count / edgeCount;
    }

    /// <summary>
    ///     The computed linear order of the vertices (the row/column order of the reordered DSM).
    ///     Vertices earlier in the list are depended on <em>less</em> from later ones; a perfect DAG
    ///     is a topological order where every edge points forward in this list.
    /// </summary>
    public IReadOnlyList<string> Order { get; }

    /// <summary>Distinct directed edges the analysis ran on (deduplicated, self edges dropped).</summary>
    public int EdgeCount { get; }

    /// <summary>
    ///     Edges that point backward in <see cref="Order" /> (above the diagonal of the reordered DSM).
    ///     This is an approximate minimum feedback arc set: removing or reversing exactly these edges
    ///     would make the graph acyclic. Every feedback edge lives inside a dependency cycle.
    /// </summary>
    public IReadOnlyCollection<(string Source, string Target)> FeedbackEdges { get; }

    /// <summary>
    ///     Tangledness in [0,1]: the share of all dependencies that unavoidably point backward no matter how
    ///     the types are ordered — <c>FeedbackEdges / EdgeCount</c>. 0 = perfectly layerable DAG,
    ///     higher = more of the system's dependencies fight a clean layering.
    /// </summary>
    public double FeedbackDensity { get; }
}

/// <summary>
///     Orders a directed graph to minimize the number of "backward" edges (the feedback arc set), then
///     reports what fraction of edges remains backward. Intuition: sort the dependency matrix (DSM) so
///     that all dependencies point below the diagonal — a cleanly layered system becomes triangular
///     and leaves nothing above the diagonal; a tangled one cannot, and the leftover above-diagonal
///     entries are the measure of its tangledness.
///     <para>
///         The ordering uses the Eades-Lin-Smyth greedy heuristic (1993): repeatedly peel off sinks
///         (to the right) and sources (to the left), and when neither exists, take the vertex with the
///         largest out-degree minus in-degree. Exact minimum feedback arc set is NP-hard; this greedy
///         is linear-ish and guarantees a feedback set of at most <c>m/2 - n/6</c>, which is tight
///         enough for a system-level trend metric. It runs on the shared, prebuilt
///         <see cref="TypeGraph" /> (its out/in adjacency), so no graph is rebuilt here.
///     </para>
/// </summary>
public static class FeedbackArcAnalysis
{
    public static FeedbackArcResult Analyze(TypeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var order = OrderByEadesLinSmyth(graph.Vertices, graph.Out, graph.In);

        var position = new Dictionary<string, int>(order.Count);
        for (var i = 0; i < order.Count; i++)
        {
            position[order[i]] = i;
        }

        // Any edge that runs from a later to an earlier vertex points backward against the ordering.
        var feedbackEdges = new List<(string Source, string Target)>();
        foreach (var source in graph.Vertices)
        {
            foreach (var target in graph.Out[source])
            {
                if (position[source] > position[target])
                {
                    feedbackEdges.Add((source, target));
                }
            }
        }

        return new FeedbackArcResult(order, graph.EdgeCount, feedbackEdges);
    }

    /// <summary>
    ///     Greedy vertex sequencing (Eades, Lin, Smyth 1993). Builds the order from both ends: sinks are
    ///     appended to the right block, sources prepended to the left block, and when the remaining graph
    ///     has neither, the vertex maximizing (out-degree - in-degree) among the remaining vertices is
    ///     placed on the left. Degrees are tracked incrementally as vertices are removed.
    /// </summary>
    private static List<string> OrderByEadesLinSmyth(
        IReadOnlySet<string> vertices,
        IReadOnlyDictionary<string, HashSet<string>> outSet,
        IReadOnlyDictionary<string, HashSet<string>> inSet)
    {
        var remaining = new HashSet<string>(vertices);
        var outDeg = vertices.ToDictionary(v => v, v => outSet[v].Count);
        var inDeg = vertices.ToDictionary(v => v, v => inSet[v].Count);

        var left = new List<string>(); // sources side, in final order
        var right = new List<string>(); // sinks side, collected reversed then flipped

        var sinks = new Queue<string>(remaining.Where(v => outDeg[v] == 0));
        var sources = new Queue<string>(remaining.Where(v => outDeg[v] != 0 && inDeg[v] == 0));

        void Remove(string u)
        {
            remaining.Remove(u);
            foreach (var pred in inSet[u])
            {
                if (!remaining.Contains(pred))
                {
                    continue;
                }

                if (--outDeg[pred] == 0)
                {
                    sinks.Enqueue(pred); // became a sink
                }
            }

            foreach (var succ in outSet[u])
            {
                if (!remaining.Contains(succ))
                {
                    continue;
                }

                if (--inDeg[succ] == 0 && outDeg[succ] > 0)
                {
                    sources.Enqueue(succ); // became a source (and not already a sink)
                }
            }
        }

        while (remaining.Count > 0)
        {
            var progressed = false;

            while (sinks.Count > 0)
            {
                var u = sinks.Dequeue();
                if (!remaining.Contains(u) || outDeg[u] != 0)
                {
                    continue; // stale queue entry
                }

                right.Add(u);
                Remove(u);
                progressed = true;
            }

            while (sources.Count > 0)
            {
                var u = sources.Dequeue();
                if (!remaining.Contains(u) || inDeg[u] != 0 || outDeg[u] == 0)
                {
                    continue; // stale, or it turned into a sink meanwhile
                }

                left.Add(u);
                Remove(u);
                progressed = true;
            }

            if (remaining.Count == 0)
            {
                break;
            }

            if (progressed)
            {
                continue; // peeling may have exposed fresh sinks/sources
            }

            // Only cyclic vertices left: pick the one that most "wants" to be a source.
            var pick = remaining
                .OrderByDescending(v => outDeg[v] - inDeg[v])
                .ThenBy(v => v, StringComparer.Ordinal) // deterministic tie-break
                .First();

            left.Add(pick);
            Remove(pick);
        }

        right.Reverse();
        left.AddRange(right);
        return left;
    }
}
