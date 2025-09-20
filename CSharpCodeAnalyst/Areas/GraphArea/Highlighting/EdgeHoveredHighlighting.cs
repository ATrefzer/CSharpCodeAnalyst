using Contracts.Graph;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.GraphArea.Highlighting;

internal class EdgeHoveredHighlighting : HighlightingBase
{
    public override void Clear(Microsoft.Msagl.WpfGraphControl.GraphViewer? graphViewer)
    {
        ClearAllEdges(graphViewer);
    }

    public override void Highlight(Microsoft.Msagl.WpfGraphControl.GraphViewer? graphViewer,
        IViewerObject? viewerObject, CodeGraph? codeGraph)
    {
        if (graphViewer is null || codeGraph is null)
        {
            return;
        }

        // Reset last highlighted edge, even if edge is null
        Clear(graphViewer);

        if (viewerObject is not IViewerEdge newEdge)
        {
            return;
        }

        // Highlight new edge, if any
        Highlight(newEdge);
        graphViewer.Invalidate(newEdge);
    }
}