using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Algorithms.Cycles;

public static class SearchGraphBuilder
{
    /// <summary>
    ///     Builds a search graph from the code graph for cycle detection.
    ///     By default, external elements are excluded from cycle analysis.
    /// </summary>
    public static SearchGraph BuildSearchGraph(Graph.CodeGraph codeGraph, bool includeExternal = false)
    {
        var searchNodes = new Dictionary<string, SearchNode>();

        // First pass: Copy relevant code elements over to the search graph
        // External elements are excluded by default as they cannot participate in internal cycles
        foreach (var element in codeGraph.Nodes.Values)
        {
            if (!includeExternal && element.IsExternal)
            {
                continue; // Skip external elements
            }

            var searchNode = new SearchNode(element.Id, element);
            searchNodes[element.Id] = searchNode;
        }

        // Second pass: Add dependencies for the search graph
        var allDependencies = codeGraph.Nodes.Values
            .SelectMany(c => c.Relationships)
            .Where(r => RelationshipClassifier.IsRelationshipRelevantForCycle(codeGraph, r));

        foreach (var dependency in allDependencies)
        {
            if (!includeExternal)
            {
                var sourceCodeElement = codeGraph.Nodes[dependency.SourceId];
                var targetCodeElement = codeGraph.Nodes[dependency.TargetId];
                if (sourceCodeElement.IsExternal || targetCodeElement.IsExternal)
                {
                    // Skip dependencies involving external elements
                    continue;
                }
            }

            var highest = GetHighestElementsInvolvedInDependency(codeGraph, dependency);
            if (highest is null)
            {
                // Partial graph (e.g. a "find relationships" result or a restored session):
                // the dependency cannot be lifted to a common container level within the
                // visible hierarchy, so it cannot form a cycle here -> skip it.
                continue;
            }

            var (source, target) = highest.Value;

            // In a partial graph the lifted endpoints are not guaranteed to be nodes of this
            // graph; skip such a dependency instead of failing.
            if (!searchNodes.TryGetValue(source.Id, out var searchSource) ||
                !searchNodes.TryGetValue(target.Id, out var searchTarget))
            {
                continue;
            }

            if (source.Id == target.Id && !IsMethod(codeGraph, source.Id))
            {
                // Skip all self references. They are irrelevant for cycle analysis
                // and make the CodeGraphBuilder much more complicated.
                // We can still see self references in the explorer view.
                continue;
            }

            searchSource.Dependencies.Add(searchTarget);
        }

        return new SearchGraph(searchNodes.Values.ToList());
    }

    private static bool IsMethod(Graph.CodeGraph codeGraph, string id)
    {
        return codeGraph.Nodes[id].ElementType == CodeElementType.Method;
    }

    /// <summary>
    ///     Lifts a relationship to the highest common container level (e.g. a method→method
    ///     call to the enclosing class/namespace) so cycles are detected at that level.
    ///     Returns <c>null</c> when the graph does not contain the ancestor chain required to
    ///     reach a common level — that happens for partial working graphs (find relationships,
    ///     restored sessions) and means the dependency is not cycle-relevant within the graph.
    /// </summary>
    private static (CodeElement Source, CodeElement Target)? GetHighestElementsInvolvedInDependency(
        Graph.CodeGraph codeGraph, Relationship relationship)
    {
        var source = codeGraph.Nodes[relationship.SourceId];
        var target = codeGraph.Nodes[relationship.TargetId];

        if (source.Id == target.Id)
        {
            return (source, target);
        }

        // Important we start at the method level,
        // even if we are interested in cycles on class level or higher.
        var sourcePath = source.GetPathToRoot(true).ToArray();
        var targetPath = target.GetPathToRoot(true).ToArray();

        // Find the least common ancestor. Index 0 = assembly.
        var sourceIndex = 0;
        var targetIndex = 0;
        while (sourcePath[sourceIndex].Equals(targetPath[targetIndex]))
        {
            if (sourceIndex + 1 < sourcePath.Length)
            {
                sourceIndex++;
            }

            if (targetIndex + 1 < targetPath.Length)
            {
                targetIndex++;
            }
        }

        // Remove the common ancestors.
        var highestSource = sourcePath[sourceIndex];
        var highestTarget = targetPath[targetIndex];

        // Use 1 of GetContainerLevel (see Documentation/cycle-detection.md, "The role of GetContainerLevel"):
        // After removing the common ancestors the two endpoints may sit at different hierarchy ranks
        // (e.g. a type on one side, a namespace on the other). A cycle is only meaningful between peers,
        // so we lift the lower-ranked endpoint up until both share the same rank. The result is that every
        // proxy edge runs namespace<->namespace or type<->type, never diagonally.
        var sourceLevel = CodeElementClassifier.GetContainerLevel(highestSource.ElementType);
        var targetLevel = CodeElementClassifier.GetContainerLevel(highestTarget.ElementType);
        while (sourceLevel != targetLevel)
        {
            // We may climb up multiple times.
            if (sourceLevel < targetLevel)
            {
                highestSource = highestSource.Parent;
            }
            else
            {
                highestTarget = highestTarget.Parent;
            }

            if (highestSource is null || highestTarget is null)
            {
                // Climbed above the part of the hierarchy contained in this graph: the
                // dependency cannot be lifted to a common level here (partial graph). The
                // caller skips it. On the complete (parsed) graph this never happens.
                return null;
            }

            sourceLevel = CodeElementClassifier.GetContainerLevel(highestSource.ElementType);
            targetLevel = CodeElementClassifier.GetContainerLevel(highestTarget.ElementType);
        }

        return (highestSource, highestTarget);
    }
}