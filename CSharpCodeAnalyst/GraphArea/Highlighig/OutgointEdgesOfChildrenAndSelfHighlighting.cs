using Contracts.Graph;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;

namespace CSharpCodeAnalyst.GraphArea.Highlighig
{
    class OutgointEdgesOfChildrenAndSelfHighlighting : HighlightingBase
    {
        public override void Clear(GraphViewer? graphViewer)
        {
            ClearAllEdges(graphViewer);
        }

        public override void Highlight(GraphViewer? graphViewer, IViewerObject? viewerObject, CodeGraph? codeGraph)
        {
            if (graphViewer is null || codeGraph is null)
            {
                return;
            }

            if (viewerObject is not IViewerNode node)
            {
                ClearAllEdges(graphViewer);
                return;
            }

            var ids = new HashSet<string>();
            if (node != null)
            {
                // TODO atr How to iterate the graph?
                var id = node.Node.Id;
                var vertex = codeGraph.Nodes[id];
                ids = vertex.GetChildrenIncludingSelf();
            }

            var edges = graphViewer.Entities.OfType<IViewerEdge>();
            foreach (var edge in edges)
            {
                var sourceId = edge.Edge.Source;
                if (ids.Contains(sourceId))
                {
                    Highlight(edge);
                }
                else
                {
                    ClearHighlight(edge);
                }
            }
        }
    }
}