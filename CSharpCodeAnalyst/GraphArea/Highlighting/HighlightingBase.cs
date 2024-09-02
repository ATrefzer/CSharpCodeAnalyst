using Contracts.Graph;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;

namespace CSharpCodeAnalyst.GraphArea.Highlighting;

internal abstract class HighlightingBase : IHighlighting
{
    private readonly Color _highlightColor = Color.Red;
    private readonly int _highlightWeight = 3;

    private readonly Color _normalColor = Color.Black;
    private readonly int _normalWeight = 1;
    public abstract void Highlight(GraphViewer? graphViewer, IViewerObject? viewerObject, CodeGraph? codeGraph);

    public abstract void Clear(GraphViewer? graphViewer);

    protected void Highlight(IViewerEdge edge)
    {
        edge.Edge.Attr.Color = _highlightColor;
        edge.Edge.Attr.LineWidth = _highlightWeight;
    }

    protected void ClearHighlight(IViewerEdge? edge)
    {
        if (edge is null)
        {
            return;
        }

        edge.Edge.Attr.Color = _normalColor;
        edge.Edge.Attr.LineWidth = _normalWeight;
    }

    protected void ClearAllEdges(GraphViewer? graphViewer)
    {
        if (graphViewer is null)
        {
            return;
        }

        var edges = graphViewer.Entities.OfType<IViewerEdge>();
        foreach (var edge in edges)
        {
            ClearHighlight(edge);
        }
    }
}