using CodeParser.Analysis.Shared;
using Contracts.Graph;

namespace CodeParser.Analysis.Cycles;

public class CodeGraphBuilder
{
    /// <summary>
    ///     The search graph is a subset of the original graph that contains only
    ///     the containers (class, namespaces etc.) that are involved in a cycle.
    ///     Returns a code graph that includes these element but also their dependencies
    ///     that were collapsed during cycle detection.
    /// </summary>
    public static CodeGraph GenerateDetailedCodeGraph(List<SearchNode> searchGraph, CodeGraph originalGraph)
    {
        var detailedGraph = new CodeGraph();
        foreach (var searchGraphSource in searchGraph)
        {
            var proxySource = searchGraphSource.OriginalElement;
            detailedGraph.IntegrateCodeElementFromOriginal(proxySource);

            // All edges in the search graph are expanded with equivalent edges in the original graph
            var allSources = proxySource.GetChildrenIncludingSelf();

            foreach (var searchGraphDependency in searchGraphSource.Dependencies)
            {
                var proxyTarget = searchGraphDependency.OriginalElement;
                var sources = allSources;
                var targets = proxyTarget.GetChildrenIncludingSelf();

                // Handle cases where a code element is a child of another.
                // We have to take care not to include unwanted dependencies.
                // Only dependencies that cross the containers are valid.
                if (proxySource.IsParentOf(proxyTarget))
                {
                    sources = [proxySource.Id];

                    // For an example why this line is needed: See Regression_NestedNamespaces
                    // Sources are only those elements with a lower container level.
                    var parentLevel = CodeElementClassifier.GetContainerLevel(proxySource.ElementType);
                    var children = proxySource.Children.Where(c =>
                        CodeElementClassifier.GetContainerLevel(c.ElementType) < parentLevel);
                    foreach (var child in children)
                    {
                        sources.UnionWith(child.GetChildrenIncludingSelf());
                    }

                    sources = sources.Except(targets).ToHashSet();
                }

                if (proxyTarget.IsParentOf(proxySource))
                {
                    targets = [proxyTarget.Id];
                    var parentLevel = CodeElementClassifier.GetContainerLevel(proxyTarget.ElementType);
                    var children = proxyTarget.Children.Where(c =>
                        CodeElementClassifier.GetContainerLevel(c.ElementType) < parentLevel);
                    foreach (var child in children)
                    {
                        targets.UnionWith(child.GetChildrenIncludingSelf());
                    }

                    targets = targets.Except(sources).ToHashSet();
                }

                var originalDependencies = GetOriginalDependencies(originalGraph, sources, targets);
                foreach (var originalDependency in originalDependencies)
                {
                    // Ensure the vertices exist
                    var source =
                        detailedGraph.IntegrateCodeElementFromOriginal(
                            originalGraph.Nodes[originalDependency.SourceId]);

                    detailedGraph.IntegrateCodeElementFromOriginal(
                        originalGraph.Nodes[originalDependency.TargetId]);

                    // Include dependency
                    source.Dependencies.Add(originalDependency);
                }
            }
        }

        FillContainerGaps(originalGraph, detailedGraph);
        return detailedGraph;
    }

    private static List<Dependency> GetOriginalDependencies(CodeGraph originalGraph, HashSet<string> sources,
        HashSet<string> targets)
    {
        // All original edges causing the same dependency as proxy used in search graph.

        var sourceElements = sources.Select(s => originalGraph.Nodes[s]);
        var fromSource = sourceElements.SelectMany(s => s.Dependencies);
        var originalDependencies = fromSource
            .Where(d => targets.Contains(d.TargetId))
            .Where(d => DependencyClassifier.IsDependencyRelevantForCycle(originalGraph, d))
            .ToList();

        // Performance nightmare
        //var originalDependencies_old = allDependencies
        //    .Where(d => sources.Contains(d.SourceId) && targets.Contains(d.TargetId)).ToList();

        return originalDependencies;
    }

    /// <summary>
    ///     If a parent and an indirect child are already included in the detailed graph,
    ///     then we fill the gap by adding all intermediate containers.
    ///     Otherwise, the detailed graph with the cycles would be confusing.
    /// </summary>
    private static void FillContainerGaps(CodeGraph originalGraph, CodeGraph detailedGraph)
    {
        // detailedGraph.Nodes gets modified during iteration
        var validIds = detailedGraph.Nodes.Keys.ToHashSet();
        var vertices = detailedGraph.Nodes.Values.ToList();

        foreach (var vertex in vertices)
        {
            var include = false;
            var originalVertex = originalGraph.Nodes[vertex.Id];
            var originalPath = originalVertex.GetPathToRoot(false);
            foreach (var parent in originalPath)
            {
                if (validIds.Contains(parent.Id))
                {
                    include = true;
                }

                if (include)
                {
                    detailedGraph.IntegrateCodeElementFromOriginal(originalGraph.Nodes[parent.Id]);
                }
            }
        }
    }
}