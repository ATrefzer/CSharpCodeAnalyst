using Contracts.GraphInterface;

namespace CodeParser.Analysis.Shared;

/// <summary>
///     Returns the strongly connected components.
///     Cycles are restricted by these components.
/// </summary>
public class Scc<TVertex>
{
    public HashSet<TVertex> Vertices = [];
}

public static class Tarjan
{
    /// <summary>
    ///     O(|E| + |V|)
    /// </summary>
    public static List<Scc<TVertex>> FindStronglyConnectedComponents<TVertex>(IGraphRepresentation<TVertex> graph)
        where TVertex : notnull
    {
        var sccs = new List<Scc<TVertex>>();

        var idMap = new Dictionary<TVertex, int>();
        var lowMap = new Dictionary<TVertex, int>();
        var stack = new Stack<TVertex>();
        var inStack = new HashSet<TVertex>();

        foreach (var vertex in graph.GetVertices())
        {
            if (!idMap.ContainsKey(vertex))
            {
                Dfs(graph, vertex, idMap, lowMap, stack, inStack, sccs);
            }
        }

        return sccs;
    }

    private static void Dfs<TVertex>(IGraphRepresentation<TVertex> graph,
        TVertex u,
        Dictionary<TVertex, int> idMap,
        Dictionary<TVertex, int> lowMap,
        Stack<TVertex> stack,
        HashSet<TVertex> inStack, // visited
        List<Scc<TVertex>> sccs) where TVertex : notnull


    {
        // Next available Id
        idMap[u] = idMap.Count;
        lowMap[u] = idMap[u];
        stack.Push(u);
        inStack.Add(u);

        foreach (var v in graph.GetNeighbors(u))
        {
            if (!idMap.ContainsKey(v))
            {
                // Unvisited vertex
                Dfs(graph, v, idMap, lowMap, stack, inStack, sccs);
                lowMap[u] = Math.Min(lowMap[u], lowMap[v]);
            }
            else if (inStack.Contains(v))
            {
                // Back edge, don't call dfs but update low link id.
                lowMap[u] = Math.Min(lowMap[u], lowMap[v]);
            }
            // Cross edge. Vertex already belongs to an SCC.
        }

        if (lowMap[u] == idMap[u])
        {
            // Vertex is root of SCC.
            var scc = new Scc<TVertex>();
            while (stack.Any())
            {
                var popped = stack.Pop();
                inStack.Remove(popped);
                scc.Vertices.Add(popped);
                if (popped.Equals(u))
                {
                    break;
                }
            }

            sccs.Add(scc);
        }
    }
}