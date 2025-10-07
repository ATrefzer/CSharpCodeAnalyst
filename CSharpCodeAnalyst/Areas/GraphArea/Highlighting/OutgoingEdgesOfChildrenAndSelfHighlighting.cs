using Contracts.Graph;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea.Highlighting;

internal class OutgoingEdgesOfChildrenAndSelfHighlighting : HighlightingBase
{
    public override void Highlight(IGraphViewerHighlighting graphViewer,
        IViewerObject? viewerObject, CodeGraph? codeGraph)
    {
        var msagl = graphViewer.GetMsaglGraphViewer();
        if (codeGraph is null || msagl is null)
        {
            return;
        }

        if (viewerObject is not IViewerNode node)
        {
            graphViewer.ClearAllEdgeHighlighting();
            return;
        }

        var id = node.Node.Id;
        var vertex = codeGraph.Nodes[id];
        var ids = vertex.GetChildrenIncludingSelf();

        var edges = msagl.Entities.OfType<IViewerEdge>();
        foreach (var edge in edges)
        {
            var sourceId = edge.Edge.Source;
            if (ids.Contains(sourceId))
            {
                graphViewer.HighlightEdge(edge);
            }
            else
            {
                graphViewer.ClearEdgeHighlighting(edge);
            }
        }
    }
}