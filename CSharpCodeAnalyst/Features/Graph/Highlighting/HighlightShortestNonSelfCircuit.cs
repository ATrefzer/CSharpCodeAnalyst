using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea.Highlighting;

internal class HighlightShortestNonSelfCircuit : HighlightingBase
{
    private Dictionary<string, IViewerNode> _idToViewerNode = new();
    private Graph? _lastGraph;


    private void Clear(IGraphViewerHighlighting graphViewer)
    {
        graphViewer.ClearAllEdgeHighlighting();
        _idToViewerNode.Clear();
        _lastGraph = null;
    }


    public override void Highlight(IGraphViewerHighlighting graphViewer,
        IViewerObject? viewerObject, CodeGraph.Graph.CodeGraph? codeGraph)
    {
        if (codeGraph is null)
        {
            return;
        }

        if (viewerObject is not IViewerNode selectedNode)
        {
            Clear(graphViewer);
            return;
        }

        var msagl = graphViewer.GetMsaglGraphViewer();
        if (msagl != null && !ReferenceEquals(_lastGraph, msagl.Graph))
        {
            // Optimize same search on same graph. Did it really take that long?
            _idToViewerNode = msagl.Entities.OfType<IViewerNode>().ToDictionary(n => n.Node.Id, n => n);
            _lastGraph = msagl.Graph;
        }

        var shortestPath = new List<IViewerEdge>();
        var minEdges = int.MaxValue;

        // We see the graph in correct representation state (collapsed nodes)
        foreach (var edgeToNeighbor in selectedNode.OutEdges)
        {
            // Since we don't want self edge we start with the direct neighbors
            // Edge.Source is null!?

            var path = BreadthFirstSearch(
                _idToViewerNode[edgeToNeighbor.Edge.Target], selectedNode, _idToViewerNode);

            if (path.Any() && path.Count + 1 < minEdges) // + 1 for the starting edge
            {
                path.Add(edgeToNeighbor);
                shortestPath = path;
                minEdges = path.Count;
            }
        }

        graphViewer.ClearAllEdgeHighlighting();
        foreach (var edge in shortestPath)
        {
            graphViewer.HighlightEdge(edge);
        }
    }


    private static List<IViewerEdge> BreadthFirstSearch(IViewerNode start, IViewerNode end,
        Dictionary<string, IViewerNode> idToViewerNode)
    {
        // node id -> edge we came from (contains the source)
        var whereICameFrom = new Dictionary<string, IViewerEdge>();
        var queue = new Queue<IViewerNode>();
        queue.Enqueue(start);
        whereICameFrom[start.Node.Id] = null!;

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (ReferenceEquals(node, end))
            {
                return Backtrace(whereICameFrom, start.Node.Id, end.Node.Id);
            }

            foreach (var outEdge in node.OutEdges)
            {
                if (whereICameFrom.TryAdd(outEdge.Edge.Target, outEdge))
                {
                    queue.Enqueue(idToViewerNode[outEdge.Edge.Target]);
                }
            }
        }

        return []; // No path found
    }

    /// <summary>
    ///     Called to recreate the path when we know it exists
    ///     Returns the list of edges to get from start to end.
    /// </summary>
    private static List<IViewerEdge> Backtrace(Dictionary<string, IViewerEdge> whereICameFrom, string startNodeId,
        string endNodeId)
    {
        // Add immediately so we can use it later for self edges.
        var path = new List<IViewerEdge>
            { whereICameFrom[endNodeId] };

        var currentNodeId = path.Last().Edge.Source;
        while (currentNodeId != startNodeId)
        {
            var incomingEdge = whereICameFrom[currentNodeId];
            path.Add(incomingEdge);
            currentNodeId = incomingEdge.Edge.Source;
        }

        // Not necessary.
        //path.Reverse();
        return path;
    }
}