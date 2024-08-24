using Contracts.Graph;

namespace CodeParser.Extensions;

/// <summary>
///     Set of basic algorithms to build for higher algorithms
/// </summary>
public static class CodeGraphExtensions
{
    /// <summary>
    ///     Clones the given code graph.
    ///     Dependencies and code element can be filtered to generate sub graphs.
    ///     If no code element list is given (null) all code elements are returned.
    /// </summary>
    public static CodeGraph Clone(this CodeGraph originalCodeGraph, Func<Dependency, bool>? dependencyFilter,
        HashSet<string>? codeElementIds)
    {
        List<CodeElement> includedOriginalElements;
        if (codeElementIds is null)
        {
            includedOriginalElements = originalCodeGraph.Nodes.Values.ToList();
        }
        else
        {
            includedOriginalElements = originalCodeGraph.Nodes.Values
                .Where(n => codeElementIds.Contains(n.Id))
                .ToList();
        }

        var clonedCodeStructure = new CodeGraph();


        // First pass: Create all elements without setting relationships
        foreach (var originalElement in includedOriginalElements)
        {
            var clonedElement = CloneElement(originalElement);
            clonedCodeStructure.Nodes[clonedElement.Id] = clonedElement;
        }

        // Second pass: Set relationships (parent / child / dependencies)
        foreach (var originalElement in includedOriginalElements)
        {
            var clonedElement = clonedCodeStructure.Nodes[originalElement.Id];

            // Set parent
            if (originalElement.Parent != null)
            {
                // Note that we may lose the parent!
                var parent = clonedCodeStructure.TryGetCodeElement(originalElement.Parent?.Id);
                clonedElement.Parent = parent;
            }

            // Set children
            foreach (var originalChild in originalElement.Children)
            {
                var clonedChild = clonedCodeStructure.TryGetCodeElement(originalChild.Id);
                if (clonedChild is null)
                {
                    continue;
                }

                clonedElement.Children.Add(clonedChild);
            }

            // Set dependencies
            foreach (var originalDependency in originalElement.Dependencies)
            {
                if (dependencyFilter == null || dependencyFilter(originalDependency))
                {
                    if (clonedCodeStructure.Nodes.ContainsKey(originalDependency.TargetId))
                    {
                        clonedElement.Dependencies.Add(new Dependency(
                            clonedElement.Id,
                            originalDependency.TargetId,
                            originalDependency.Type
                        ));
                    }
                }
            }
        }

        return clonedCodeStructure;
    }

    private static CodeElement CloneElement(CodeElement originalElement)
    {
        return originalElement.CloneSimple();
    }

    public static CodeGraph SubGraphOf(this CodeGraph graph, HashSet<string> codeElementIds)
    {
        // Include only dependencies to code elements in the subgraph
        return graph.Clone(d => codeElementIds.Contains(d.TargetId), codeElementIds);
    }


    public static void RemoveCodeElementAndAllChildren(this CodeGraph graph, string codeElementIds)
    {
        var element = graph.TryGetCodeElement(codeElementIds);
        if (element is null)
        {
            return;
        }

        var elementIdsToRemove = element.GetChildrenIncludingSelf().ToHashSet();
        graph.RemoveCodeElements(elementIdsToRemove);
    }
}