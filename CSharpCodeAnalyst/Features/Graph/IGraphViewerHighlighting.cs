using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea;

public interface IGraphViewerHighlighting
{
    Microsoft.Msagl.WpfGraphControl.GraphViewer? GetMsaglGraphViewer();

    void ClearAllEdgeHighlighting();

    /// <summary>
    ///     Since edges are highlighted when the mouse hovers over it we have to
    ///     recover the flags if the highlighting is cleared.
    /// </summary>
    void ClearEdgeHighlighting(IViewerEdge? edge);

    void HighlightEdge(IViewerEdge edge);
}