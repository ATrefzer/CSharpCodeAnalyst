using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;

/// <summary>
///     The deduplicated, type-level dependency graph the system metrics share. One vertex per internal
///     type, one edge per distinct (source type -> target type) dependency: the fine-grained
///     relationships (calls, field uses, ...) are lifted to their containing type, duplicates are
///     collapsed, and self edges and external types are dropped — exactly the graph
///     <see cref="TypeDependencyAnalysis" /> describes. Both the outgoing and the incoming adjacency are
///     materialized, so a metric can walk the graph in either direction without rebuilding it.
///     <para>
///         Built once via <see cref="Build" /> and passed to every metric stage
///         (<see cref="SystemMetricsAnalysis" />), which is why the lift/dedup work happens a single time.
///     </para>
/// </summary>
public sealed class TypeGraph
{
    private TypeGraph(
        HashSet<string> vertices,
        Dictionary<string, HashSet<string>> outgoing,
        Dictionary<string, HashSet<string>> incoming,
        int edgeCount)
    {
        Vertices = vertices;
        Out = outgoing;
        In = incoming;
        EdgeCount = edgeCount;
    }

    /// <summary>The internal types the graph is built from (the vertices).</summary>
    public IReadOnlySet<string> Vertices { get; }

    /// <summary>Outgoing adjacency: <c>Out[a]</c> contains <c>b</c> when a depends on b.</summary>
    public IReadOnlyDictionary<string, HashSet<string>> Out { get; }

    /// <summary>Incoming adjacency: <c>In[b]</c> contains <c>a</c> when a depends on b. The reverse of <see cref="Out" />.</summary>
    public IReadOnlyDictionary<string, HashSet<string>> In { get; }

    /// <summary>Number of distinct directed edges (deduplicated, self edges dropped).</summary>
    public int EdgeCount { get; }

    public int VertexCount => Vertices.Count;

    /// <summary>
    ///     Builds the type graph from a code graph: keeps the internal types, lifts every dependency
    ///     relationship to its containing type and deduplicates.
    /// </summary>
    public static TypeGraph Build(Graph.CodeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var vertices = graph.Nodes.Values
            .Where(n => n.IsType() && !n.IsExternal)
            .Select(n => n.Id)
            .ToHashSet();

        return FromEdges(vertices, LiftedTypeEdges(graph));
    }

    /// <summary>
    ///     Builds the type graph from an already type-level adjacency list (vertices plus outgoing
    ///     targets). Deduplicates, drops self edges and edges whose endpoints are not vertices. Handy for
    ///     tests and callers that already hold a raw adjacency rather than a <see cref="Graph.CodeGraph" />.
    /// </summary>
    public static TypeGraph FromAdjacency(
        IReadOnlyCollection<string> vertices,
        IReadOnlyDictionary<string, List<string>> outgoing)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(outgoing);

        var vertexSet = vertices as HashSet<string> ?? [..vertices];
        return FromEdges(vertexSet, FlattenAdjacency(outgoing));
    }

    private static IEnumerable<(string Source, string Target)> LiftedTypeEdges(Graph.CodeGraph graph)
    {
        foreach (var relationship in graph.GetAllRelationships())
        {
            if (!relationship.Type.IsDependency())
            {
                continue;
            }

            var source = ContainingType(graph, relationship.SourceId);
            var target = ContainingType(graph, relationship.TargetId);
            if (source is null || target is null)
            {
                continue;
            }

            // Endpoints outside the vertex set (external types) and self edges are filtered in FromEdges.
            yield return (source.Id, target.Id);
        }
    }

    private static IEnumerable<(string Source, string Target)> FlattenAdjacency(
        IReadOnlyDictionary<string, List<string>> outgoing)
    {
        foreach (var (source, targets) in outgoing)
        {
            foreach (var target in targets)
            {
                yield return (source, target);
            }
        }
    }

    /// <summary>
    ///     Core builder: turns a stream of possibly duplicated / self / external type-level edges into
    ///     the deduplicated in/out adjacency. Endpoints outside <paramref name="vertices" /> and self
    ///     edges are dropped; the edge count is the number of distinct surviving edges.
    /// </summary>
    private static TypeGraph FromEdges(HashSet<string> vertices, IEnumerable<(string Source, string Target)> edges)
    {
        var outgoing = vertices.ToDictionary(v => v, _ => new HashSet<string>());
        var incoming = vertices.ToDictionary(v => v, _ => new HashSet<string>());
        var edgeCount = 0;

        foreach (var (source, target) in edges)
        {
            if (source == target || !vertices.Contains(source) || !vertices.Contains(target))
            {
                // Remove self edges
                continue;
            }

            if (outgoing[source].Add(target))
            {
                incoming[target].Add(source);
                edgeCount++;
            }
        }

        return new TypeGraph(vertices, outgoing, incoming, edgeCount);
    }

    /// <summary>
    ///     Returns the type that contains the given element (a type element maps to itself), or null if
    ///     the element sits above the type level (namespace, assembly).
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
