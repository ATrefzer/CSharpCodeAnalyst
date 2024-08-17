namespace CodeParser.Analysis.Shared;

public class Scc
{
    public HashSet<SearchNode> Vertices { get; } = [];
}

public static class Tarjan
{
    /// <summary>
    ///     O(|E| + |V|)
    /// </summary>
    public static List<Scc> FindStronglyConnectedComponents(List<SearchNode> graph)
    {
        var sccs = new List<Scc>();

        var idMap = new Dictionary<SearchNode, int>();
        var lowMap = new Dictionary<SearchNode, int>();
        var stack = new Stack<SearchNode>();
        var inStack = new HashSet<SearchNode>();

        foreach (var vertex in graph)
        {
            if (!idMap.ContainsKey(vertex))
            {
                Dfs(vertex, idMap, lowMap, stack, inStack, sccs);
            }
        }

        return sccs;
    }

    private static void Dfs(SearchNode u,
        Dictionary<SearchNode, int> idMap,
        Dictionary<SearchNode, int> lowMap,
        Stack<SearchNode> stack,
        HashSet<SearchNode> inStack, // visited
        List<Scc> sccs)


    {
        // Next available Id
        idMap[u] = idMap.Count;
        lowMap[u] = idMap[u];
        stack.Push(u);
        inStack.Add(u);

        foreach (var v in u.Dependencies)
        {
            if (idMap.ContainsKey(v) is false)
            {
                // Unvisited vertex
                Dfs(v, idMap, lowMap, stack, inStack, sccs);
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
            var scc = new Scc();
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