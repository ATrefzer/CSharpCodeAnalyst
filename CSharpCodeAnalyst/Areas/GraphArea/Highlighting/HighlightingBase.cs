using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea.Highlighting;

internal abstract class HighlightingBase : IHighlighting
{
    public abstract void Highlight(IGraphViewerHighlighting graphViewer,
        IViewerObject? viewerObject, CodeGraph.Graph.CodeGraph? codeGraph);
}