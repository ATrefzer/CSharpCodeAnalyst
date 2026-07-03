using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Algorithms.Cycles;

public static class CodeGraphBuilder
{
    /// <summary>
    ///     The search graph is a subset of the original graph that contains only
    ///     the containers (class, namespaces etc.) that are involved in a cycle.
    ///     Returns a code graph that includes these element but also their dependencies
    ///     that were collapsed during cycle detection.
    /// </summary>
    public static Graph.CodeGraph GenerateDetailedCodeGraph(List<SearchNode> searchGraph, Graph.CodeGraph originalGraph)
    {
        var detailedGraph = new Graph.CodeGraph();
        foreach (var searchGraphSource in searchGraph)
        {
            var proxySource = searchGraphSource.OriginalElement;
            detailedGraph.IntegrateCodeElementFromOriginal(proxySource);

            // All edges in the search graph are expanded with equivalent edges in the original graph.
            // Note: a proxy edge stands for ALL concrete edges between its children, so an element that
            // shares the proxy's container but is not itself on the loop (e.g. a property setter that
            // writes the same backing field as its getter) is re-materialised too. This is intentional;
            // see Documentation/cycle-detection.md, "Why non-cycle edges appear in the result".
            var allSources = proxySource.GetChildrenIncludingSelf();

            foreach (var searchGraphDependency in searchGraphSource.Dependencies)
            {
                var proxyTarget = searchGraphDependency.OriginalElement;
                var sources = allSources;
                var targets = proxyTarget.GetChildrenIncludingSelf();

                // Handle cases where a code element is a child of another.
                // We have to take care not to include unwanted dependencies.
                // Only dependencies that cross the containers are valid.
                // Use 2 of GetContainerLevel (see Documentation/cycle-detection.md, "The role of GetContainerLevel"):
                // When a container points to one of its own nested containers, the parent's valid sources/targets
                // are only its directly-contained content (members and types), NOT the sibling containers of equal
                // rank that merely happen to be nested inside it. Restricting by container level cuts those off.
                // Without it, dependencies of an unrelated nested namespace would be pulled into the cycle group.
                // See Regression_NestedNamespaces for the concrete case (NS_Irrelevant must stay out).
                if (proxySource.IsParentOf(proxyTarget))
                {
                    sources = [proxySource.Id];

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
                    var sourceId = originalGraph.Nodes[originalDependency.SourceId];
                    var source = detailedGraph.IntegrateCodeElementFromOriginal(sourceId).CodeElement;

                    detailedGraph.IntegrateCodeElementFromOriginal(
                        originalGraph.Nodes[originalDependency.TargetId]);

                    // Include dependency
                    source.Relationships.Add(originalDependency);
                }
            }
        }

        FillContainerGaps(originalGraph, detailedGraph);
        return detailedGraph;
    }

    private static List<Relationship> GetOriginalDependencies(Graph.CodeGraph originalGraph, HashSet<string> sources,
        HashSet<string> targets)
    {
        // All original edges causing the same dependency as proxy used in search graph.

        var sourceElements = sources.Select(s => originalGraph.Nodes[s]);
        var fromSource = sourceElements.SelectMany(s => s.Relationships);
        var originalDependencies = fromSource
            .Where(d => targets.Contains(d.TargetId))
            .Where(d => RelationshipClassifier.IsRelationshipRelevantForCycle(originalGraph, d))
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
    private static void FillContainerGaps(Graph.CodeGraph originalGraph, Graph.CodeGraph detailedGraph)
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