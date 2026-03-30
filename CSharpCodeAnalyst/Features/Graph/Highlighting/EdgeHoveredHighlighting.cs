using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea.Highlighting;

internal class EdgeHoveredHighlighting : HighlightingBase
{

    public override void Highlight(IGraphViewerHighlighting graphViewer,
        IViewerObject? viewerObject, CodeGraph.Graph.CodeGraph? codeGraph)
    {
        if (codeGraph is null)
        {
            return;
        }

        graphViewer.ClearAllEdgeHighlighting();
        if (viewerObject is not IViewerEdge newEdge)
        {
            return;
        }

        // Highlight new edge, if any
        graphViewer.HighlightEdge(newEdge);
    }
}