using CSharpCodeAnalyst.CodeGraph.Algorithms.Cycles;
using CSharpCodeAnalyst.CodeGraph.Contracts;
using DsmSuite.DsmViewer.Application.Sorting;
using DsmSuite.DsmViewer.Model.Interfaces;

namespace CSharpCodeAnalyst.Features.DsmMatrix;

/// <summary>
///     Drop-in replacement for DsmSuite's <see cref="PartitionSortAlgorithm" />. Their
///     <c>PartitioningCalculation.ToBlockTriangular</c> is a brute-force score maximizer (up to
///     O(n^4) probed swaps per pass, each scored in O(n^2)) that becomes effectively endless for a
///     parent with a few hundred children - exactly the flat shape the jdeps and doxygen imports
///     produce. Classic DSM partitioning needs none of that: condensing the sibling dependency
///     graph to its strongly connected components and ordering the components topologically yields
///     the block triangular form directly, in O(V + E). Cycles stay together as blocks; siblings
///     inside a block and independent siblings keep their existing (alphabetical) order.
///     <para>
///         Ordering convention (matches the DsmSuite matrix, row = provider, column = consumer):
///         consumers on top, providers at the bottom, so the dependencies end up below the
///         diagonal.
///     </para>
/// </summary>
public sealed class SccPartitionSortAlgorithm : ISortAlgorithm
{
    private readonly IDsmElement _element;
    private readonly IDsmModel _model;

    public SccPartitionSortAlgorithm(IDsmModel model, IDsmElement element)
    {
        _model = model;
        _element = element;
    }

    /// <summary>
    ///     Activator-compatible constructor used by <see cref="SortAlgorithmFactory" />.
    /// </summary>
    public SccPartitionSortAlgorithm(object[] args)
        : this((IDsmModel)args[0], (IDsmElement)args[1])
    {
    }

    /// <summary>
    ///     Registered under DsmSuite's own algorithm name, so the factory (and with it the matrix
    ///     view's sort command) picks up this implementation instead of the brute force.
    /// </summary>
    public string Name
    {
        get => PartitionSortAlgorithm.AlgorithmName;
    }

    public SortResult Sort()
    {
        var children = _element.Children;
        var result = new SortResult(children.Count);
        if (children.Count <= 1)
        {
            return result;
        }

        // Edge i -> j when sibling i consumes sibling j (same weight lookup DsmSuite's
        // partitioning uses; derived weights, so nested children count as well).
        var adjacency = new List<int>[children.Count];
        for (var i = 0; i < children.Count; i++)
        {
            adjacency[i] = [];
            for (var j = 0; j < children.Count; j++)
            {
                if (i != j && _model.GetDependencyWeight(children[i], children[j]) > 0)
                {
                    adjacency[i].Add(j);
                }
            }
        }

        var sccs = Tarjan.FindStronglyConnectedComponents(new SiblingDependencyGraph(adjacency));

        var order = OrderComponentsTopologically(sccs, adjacency);

        var position = 0;
        foreach (var originalIndex in order)
        {
            result.SetIndex(position++, originalIndex);
        }

        return result;
    }

    /// <summary>
    ///     Kahn's algorithm on the condensation: a component is emitted once all of its consumers
    ///     are placed, so every dependency points from an earlier row to a later one. Ready
    ///     components are picked by their smallest original child index, which keeps independent
    ///     siblings and the members inside a cycle block in their existing order.
    /// </summary>
    private static List<int> OrderComponentsTopologically(List<Scc<int>> sccs, List<int>[] adjacency)
    {
        var componentOfVertex = new int[adjacency.Length];
        var members = new List<List<int>>(sccs.Count);
        foreach (var scc in sccs)
        {
            var sorted = scc.Vertices.OrderBy(v => v).ToList();
            foreach (var vertex in sorted)
            {
                componentOfVertex[vertex] = members.Count;
            }

            members.Add(sorted);
        }

        // Condensation edges consumer component -> provider component, deduplicated.
        var outgoing = new HashSet<int>[members.Count];
        var incomingCount = new int[members.Count];
        for (var i = 0; i < members.Count; i++)
        {
            outgoing[i] = [];
        }

        for (var source = 0; source < adjacency.Length; source++)
        {
            foreach (var target in adjacency[source])
            {
                var sourceComponent = componentOfVertex[source];
                var targetComponent = componentOfVertex[target];
                if (sourceComponent != targetComponent && outgoing[sourceComponent].Add(targetComponent))
                {
                    incomingCount[targetComponent]++;
                }
            }
        }

        var ready = new PriorityQueue<int, int>();
        for (var component = 0; component < members.Count; component++)
        {
            if (incomingCount[component] == 0)
            {
                ready.Enqueue(component, members[component][0]);
            }
        }

        var order = new List<int>(adjacency.Length);
        while (ready.TryDequeue(out var component, out _))
        {
            order.AddRange(members[component]);
            foreach (var provider in outgoing[component])
            {
                if (--incomingCount[provider] == 0)
                {
                    ready.Enqueue(provider, members[provider][0]);
                }
            }
        }

        return order;
    }

    /// <summary>Adapts the sibling adjacency (indices 0..n-1) to the Tarjan input contract.</summary>
    private sealed class SiblingDependencyGraph(List<int>[] adjacency) : IGraphRepresentation<int>
    {
        public uint VertexCount
        {
            get => (uint)adjacency.Length;
        }

        public IReadOnlyCollection<int> GetNeighbors(int vertex)
        {
            return adjacency[vertex];
        }

        public bool IsVertex(int vertex)
        {
            return vertex >= 0 && vertex < adjacency.Length;
        }

        public bool IsEdge(int source, int target)
        {
            return adjacency[source].Contains(target);
        }

        public IReadOnlyCollection<int> GetVertices()
        {
            return Enumerable.Range(0, adjacency.Length).ToList();
        }
    }
}
