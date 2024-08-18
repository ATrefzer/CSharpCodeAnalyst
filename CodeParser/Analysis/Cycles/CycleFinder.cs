using CodeParser.Analysis.Shared;
using Contracts.Graph;

namespace CodeParser.Analysis.Cycles;

public class CycleFinder
{
    public static List<CycleGroup> FindCycleGroups(CodeGraph originalGraph)
    {
        var groups = new List<CycleGroup>();

        var searchGraph = SearchGraphBuilder.BuildSearchGraph(originalGraph);
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

            groups.Add(new CycleGroup(detailedGraph));
        }

        return groups;
    }

    /// <summary>
    ///     When determining the SCC we collected the relevant nodes, but did not
    ///     remove the orphaned dependencies, yet.
    /// </summary>
    private static void RemoveOrphanedDependencies(Scc scc)
    {
        var existingIds = scc.Vertices.Select(v => v.Id).ToHashSet();
        foreach (var vertex in scc.Vertices)
        {
            vertex.Dependencies.RemoveWhere(d => !existingIds.Contains(d.Id));
        }
    }
}