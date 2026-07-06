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
    /// <summary>
    ///     Reduces the canvas to the relationships that cross the boundary of the clicked container in
    ///     one direction. For <paramref name="outgoing" /> only edges that start somewhere inside the
    ///     container (any descendant, including itself) and end outside it survive; for incoming the
    ///     reverse. Only the endpoints of those edges remain - everything that does not participate in a
    ///     crossing edge is removed. Lets you break a large dependency cycle down into "what does this
    ///     part reach out to" / "who reaches into it".
    /// </summary>
    public static GraphResult FocusOnIncomingEdges(Graph.CodeGraph graph, CodeElement element, bool outgoing)
    {
        if (!graph.Nodes.TryGetValue(element.Id, out var node))
        {
            return GraphResult.Failure();
        }

        var inside = node.GetChildrenIncludingSelf();

        bool CrossesBoundary(Relationship relationship)
        {
            var sourceInside = inside.Contains(relationship.SourceId);
            var targetInside = inside.Contains(relationship.TargetId);
            return outgoing ? sourceInside && !targetInside : !sourceInside && targetInside;
        }

        var idsToKeep = new HashSet<string>();
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

        if (idsToKeep.Count == 0)
        {
            // Nothing crosses the boundary in this direction - leave the canvas untouched.
            return GraphResult.Failure();
        }

        var newGraph = graph.Clone(CrossesBoundary, idsToKeep);
        var removedIds = graph.Nodes.Keys.Except(idsToKeep).ToHashSet();

        return new GraphResult { NewGraph = newGraph, RemovedIds = removedIds, Success = true };
    }
}