using Contracts.Graph;

namespace CodeParser.Extensions;

/// <summary>
///     Set of basic algorithms to build for higher algorithms
/// </summary>
public static class CodeGraphExtensions
{
    public static CodeGraph Clone(this CodeGraph originalCodeGraph)
    {
        return originalCodeGraph.Clone(null, null);
    }

    /// <summary>
    ///     Clones the given code graph.
    ///     Relationships and code element can be filtered to generate sub graphs.
    ///     If no code element list is given (null) all code elements are returned.
    /// </summary>
    public static CodeGraph Clone(this CodeGraph originalCodeGraph, Func<Relationship, bool>? relationshipFilter,
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

        // Second pass: Set relationships (parent / child / relationships)
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

            // Set relationships
            foreach (var originalRelationship in originalElement.Relationships)
            {
                if (relationshipFilter == null || relationshipFilter(originalRelationship))
                {
                    if (clonedCodeStructure.Nodes.ContainsKey(originalRelationship.TargetId))
                    {
                        var clonedRelationship = new Relationship(
                            clonedElement.Id,
                            originalRelationship.TargetId,
                            originalRelationship.Type,
                            originalRelationship.Attributes
                        );
                        clonedRelationship.SourceLocations.AddRange(originalRelationship.SourceLocations);
                        clonedElement.Relationships.Add(clonedRelationship);
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

    /// <summary>
    ///     Returns a subgraph with only the included elements and their associated relationships.
    /// </summary>
    public static CodeGraph SubGraphOf(this CodeGraph graph, HashSet<string> includedElements)
    {
        return graph.Clone(IncludeRelationship, includedElements);

        bool IncludeRelationship(Relationship relationship)
        {
            return includedElements.Contains(relationship.SourceId) && includedElements.Contains(relationship.TargetId);
        }
    }

    /// <summary>
    ///     Returns a subgraph with the root element and all children and associated relationships
    /// </summary>
    public static CodeGraph SubGraphOf(this CodeGraph graph, CodeElement rootElement)
    {
        var includedElements = rootElement.GetChildrenIncludingSelf();
        return SubGraphOf(graph, includedElements);
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