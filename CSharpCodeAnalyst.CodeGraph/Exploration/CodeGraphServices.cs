using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Exploration;

public class GraphResult
{
    public bool Success { get; init; }
    public Graph.CodeGraph? NewGraph { get; init; }

    public HashSet<string> RemovedIds { get; init; } = new();

    public HashSet<string> AddedIds { get; init; } = new();

    public static GraphResult Failure()
    {
        var result = new GraphResult { Success = false, NewGraph = null };
        return result;
    }
}

public static class CodeGraphServices
{

    public static GraphResult FocusOnIncomingEdges(Graph.CodeGraph graph, CodeElement originalElement)
    {
        return FocusOnCrossingEdges(graph, originalElement, false);
    }

    public static GraphResult FocusOnOutgoingEdges(Graph.CodeGraph graph, CodeElement originalElement)
    {
        return FocusOnCrossingEdges(graph, originalElement, true);
    }

    /// <summary>
    ///     Reduces the canvas to the relationships that cross the boundary of the clicked container in
    ///     one direction. For <paramref name="outgoing" /> only edges that start somewhere inside the
    ///     container (any descendant, including itself) and end outside survive. For incoming the
    ///     reverse. Only the endpoints of those edges remain - everything that does not participate in a
    ///     crossing edge is removed. Lets you break a large dependency cycle down into "what does this
    ///     part reach out to" / "who reaches into it".
    ///     <para>
    ///         <paramref name="originalElement" /> must be the clicked element from the full code graph,
    ///         not the canvas clone: the canvas may not have the full parent chain so a canvas clone may not have
    ///         children even though its members are present as free-standing nodes.
    ///         Containment is therefore decided on the original hierarchy.
    ///         No new edges appear, but for the clicked element the parent chain is completed to all existing elements in the canvas.
    ///     </para>
    /// </summary>
    public static GraphResult FocusOnCrossingEdges(Graph.CodeGraph graph, CodeElement originalElement, bool outgoing)
    {
        var inside = originalElement.GetChildrenIncludingSelf();

        bool CrossesBoundary(Relationship relationship)
        {
            var sourceInside = inside.Contains(relationship.SourceId);
            var targetInside = inside.Contains(relationship.TargetId);
            return outgoing ? sourceInside && !targetInside : !sourceInside && targetInside;
        }

        var idsToKeep = new HashSet<string> { originalElement.Id };
        foreach (var relationship in graph.GetAllRelationships())
        {
            if (CrossesBoundary(relationship))
            {
                idsToKeep.Add(relationship.SourceId);
                idsToKeep.Add(relationship.TargetId);

                // Keep also the parent chain intact.
                var source = graph.Nodes[relationship.SourceId];
                var target = graph.Nodes[relationship.TargetId];

                var parentsInGraph = source.GetPathToRoot(false)
                    .Union(target.GetPathToRoot(false)).Select(e => e.Id);

                // The parents that are already in the graph.
                idsToKeep.UnionWith(parentsInGraph);
            }
        }

        var newGraph = graph.Clone(CrossesBoundary, idsToKeep);
        var removedIds = graph.Nodes.Keys.Except(idsToKeep).ToHashSet();
        var addedIds = CompleteHierarchyToContainer(newGraph, originalElement);

        return new GraphResult { NewGraph = newGraph, RemovedIds = removedIds, AddedIds = addedIds, Success = true };
    }

    /// <summary>
    ///     Links every element of <paramref name="graph" /> that belongs to the subtree of
    ///     <paramref name="originalContainer" /> back to the container, cloning missing intermediate
    ///     containers (namespaces, outer classes) from the original hierarchy. Only the chains up to the
    ///     container are completed - nothing above or outside it is touched.
    ///     Note this is not a duplicate of ICodeGraphExplorer.FillGapsInHierarchy! />
    /// </summary>
    private static HashSet<string> CompleteHierarchyToContainer(Graph.CodeGraph graph, CodeElement originalContainer)
    {
        var addedIds = new HashSet<string>();

        // Original elements of the container's subtree by id, to walk the real parent chains.
        var originalById = originalContainer.GetSubtreeIncludingSelf().ToDictionary(e => e.Id);

        foreach (var id in graph.Nodes.Keys.ToList())
        {
            if (id == originalContainer.Id || !originalById.TryGetValue(id, out var original))
            {
                // No transitive child of original container
                continue;
            }

            var child = graph.Nodes[id];
            var originalParent = original.Parent;

            // Walk up until an existing chain continues (a parent link stops the walk; the
            // element it points to is handled by its own iteration) or the container is reached.
            while (child.Parent is null && originalParent is not null)
            {
                if (!graph.Nodes.TryGetValue(originalParent.Id, out var parent))
                {
                    parent = originalParent.CloneSimple();
                    graph.Nodes.Add(parent.Id, parent);
                    addedIds.Add(parent.Id);
                }

                child.Parent = parent;
                parent.Children.Add(child);

                if (originalParent.Id == originalContainer.Id)
                {
                    break;
                }

                child = parent;
                originalParent = originalParent.Parent;
            }
        }

        return addedIds;
    }
}