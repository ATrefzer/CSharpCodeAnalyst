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

    private readonly Color _grayColor = Color.LightGray;

    public abstract void Highlight(Microsoft.Msagl.WpfGraphControl.GraphViewer? graphViewer, IViewerObject? viewerObject, CodeGraph? codeGraph);

    public abstract void Clear(Microsoft.Msagl.WpfGraphControl.GraphViewer? graphViewer);

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

        if (edge.Edge.UserData is Relationship { Type: RelationshipType.Containment })
        {
            edge.Edge.Attr.Color = _grayColor;
        }
        else
        {
            edge.Edge.Attr.Color = _normalColor;
        }
    
        edge.Edge.Attr.LineWidth = _normalWeight;
    }

    protected void ClearAllEdges(Microsoft.Msagl.WpfGraphControl.GraphViewer? graphViewer)
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