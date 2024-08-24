using Contracts.Graph;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpCodeAnalyst.GraphArea.Highlighig
{
    internal class HighligtShortestNonSelfCircuit : HighlightingBase
    {
        Graph? _lastGraph = null;
        Dictionary<string, IViewerNode> _idToViewerNode = new Dictionary<string, IViewerNode>();


        public override void Clear(GraphViewer? graphViewer)
        {
            ClearAllEdges(graphViewer);
            _idToViewerNode.Clear();
            _lastGraph = null;
        }


        public override void Highlight(GraphViewer? graphViewer, IViewerObject? viewerObject, CodeGraph? codeGraph)
        {
            if (graphViewer is null || codeGraph is null)
            {
                return;
            }

            if (viewerObject is not IViewerNode selectedNode)
            {
                ClearAllEdges(graphViewer);
                return;
            }

            if (!ReferenceEquals(_lastGraph, graphViewer.Graph))
            {
                // Optimize same search on same graph
                _idToViewerNode = graphViewer.Entities.OfType<IViewerNode>().ToDictionary(n => n.Node.Id, n => n);
                _lastGraph = graphViewer.Graph;
            }


            List<IViewerEdge> shortestPath = new List<IViewerEdge>();
            int minEdges = int.MaxValue;
            if (selectedNode != null)
            {
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
                        minEdges = path.Count();
                    }
                }
            }

            ClearAllEdges(graphViewer);
            foreach (var edge in shortestPath)
            {
                Highlight(edge);
            }
        }
     

        public static List<IViewerEdge> BreadthFirstSearch(IViewerNode start, IViewerNode end, Dictionary<string, IViewerNode> idToViewerNode)
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
                    if (!whereICameFrom.ContainsKey(outEdge.Edge.Target))
                    {
                        whereICameFrom[outEdge.Edge.Target] = outEdge;
                        queue.Enqueue(idToViewerNode[outEdge.Edge.Target]);
                    }
                }
            }

            return new List<IViewerEdge>(); // No path found
        }

        /// <summary>
        /// Called to recreated the path when we know it exists
        /// Returns the list of edges to get from start to end.
        /// </summary>
        private static List<IViewerEdge> Backtrace(Dictionary<string, IViewerEdge> whereICameFrom, string startNodeId, string endNodeId)
        {
            // Add immediately so we can use it later for self edges.
            var edge = whereICameFrom[endNodeId];
            var path = new List<IViewerEdge>() { whereICameFrom[endNodeId] };

            string currentNodeId = path.Last().Edge.Source;
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
}
