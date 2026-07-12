using CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;

namespace CSharpCodeAnalyst.CodeGraph.Algorithms.Cycles;

public static class CycleFinder
{
    /// <summary>
    ///     Finds cycle groups in the code graph.
    /// </summary>
    public static List<CycleGroup> FindCycleGroups(Graph.CodeGraph originalGraph, bool includeExternal = false)
    {
        var groups = new List<CycleGroup>();

        var searchGraph = SearchGraphBuilder.BuildSearchGraph(originalGraph, includeExternal);
        var sccs = Tarjan.FindStronglyConnectedComponents(searchGraph);

        foreach (var scc in sccs)
        {
            if (scc.Vertices.Count < 2)
            {
                continue;
            }

            // Cleanup only cycle groups that are relevant.
            RemoveOrphanedDependencies(scc);

            var vertices = scc.Vertices.ToList();
            var detailedGraph = CodeGraphBuilder.GenerateDetailedCodeGraph(vertices, originalGraph);

            groups.Add(new CycleGroup(detailedGraph, vertices.Select(v => v.OriginalElement).ToList())
            {
                Name = CreateGroupName(detailedGraph)
            });
        }

        return groups;
    }

    /// <summary>
    ///     Names the group after its most central element (most incoming dependencies). The
    ///     sequence keeps the name stable when dependencies are removed, so a cycle can be
    ///     recognized across runs - and the same name links a NOCYCLES rule violation to the
    ///     group in the Cycles view.
    /// </summary>
    private static string CreateGroupName(Graph.CodeGraph detailedGraph)
    {
        var metrics = DependencyMetrics.Calculate(detailedGraph);

        return metrics
            .OrderByDescending(m => m.Incoming)
            .ThenByDescending(m => m.Outgoing)
            .ThenBy(m => m.Element.Name)
            .First().Element.Name;
    }

    /// <summary>
    ///     When determining the SCC we collected the relevant nodes, but did not
    ///     remove the orphaned dependencies, yet.
    /// </summary>
    private static void RemoveOrphanedDependencies(Scc<SearchNode> scc)
    {
        var existingIds = scc.Vertices.Select(v => v.Id).ToHashSet();
        foreach (var vertex in scc.Vertices)
        {
            vertex.Dependencies.RemoveWhere(d => !existingIds.Contains(d.Id));
        }
    }
}