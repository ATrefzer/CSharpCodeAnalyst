using CodeParser.Analysis.Shared;
using Contracts.Graph;

namespace CodeParser.Analysis.Cycles;

public class SearchGraphBuilder
{
    public static SearchGraph BuildSearchGraph(CodeGraph codeGraph)
    {
        var searchNodes = new Dictionary<string, SearchNode>();

        // First pass: Copy all code elements over to the search graph,
        foreach (var element in codeGraph.Nodes.Values)
        {
            var searchNode = new SearchNode(element.Id, element);
            searchNodes[element.Id] = searchNode;
        }

        // Second pass: Add dependencies for the search graph
        var allDependencies = codeGraph.Nodes.Values
            .SelectMany(c => c.Relationships)
            .Where(r => RelationshipClassifier.IsRelationshipRelevantForCycle(codeGraph, r));

        foreach (var dependency in allDependencies)
        {
            var (source, target) = GetHighestElementsInvolvedInDependency(codeGraph, dependency);
            if (source.Id == target.Id && !IsMethod(codeGraph, source.Id))
            {
                // Skip all self references. They are irrelevant for cycle analysis
                // and make the CodeGraphBuilder much more complicated.
                // We can still see self references in the explorer view.
                continue;
            }

            var searchSource = GetSearchNode(source, searchNodes);
            var searchTarget = GetSearchNode(target, searchNodes);
            searchSource.Dependencies.Add(searchTarget);
        }

        return new SearchGraph(searchNodes.Values.ToList());
    }

    private static bool IsMethod(CodeGraph codeGraph, string id)
    {
        return codeGraph.Nodes[id].ElementType == CodeElementType.Method;
    }

    private static SearchNode GetSearchNode(CodeElement element, Dictionary<string, SearchNode> searchNodes)
    {
        return searchNodes[element.Id];
    }

    private static (CodeElement, CodeElement) GetHighestElementsInvolvedInDependency(CodeGraph codeGraph,
        Relationship relationship)
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
        // However, if we end up having a proxy relationship between a namespace and a type, go up to the namespace.
        var highestSource = sourcePath[sourceIndex];
        var highestTarget = targetPath[targetIndex];

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

            CheckLogicErrors(highestSource, highestTarget);
            sourceLevel = CodeElementClassifier.GetContainerLevel(highestSource!.ElementType);
            targetLevel = CodeElementClassifier.GetContainerLevel(highestTarget!.ElementType);
        }

        return (highestSource, highestTarget);
    }

    private static void CheckLogicErrors(CodeElement? highestSource, CodeElement? highestTarget)
    {
        if (highestSource is null || highestTarget is null)
        {
            throw new IncompleteLogicException("Source or target code element in search graph is null");
        }
    }
}