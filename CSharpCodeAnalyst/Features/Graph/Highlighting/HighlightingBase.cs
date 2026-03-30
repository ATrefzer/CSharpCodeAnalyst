using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Features.Graph.Highlighting;

internal abstract class HighlightingBase : IHighlighting
{
    public abstract void Highlight(IGraphViewerHighlighting graphViewer,
        IViewerObject? viewerObject, CodeGraph.Graph.CodeGraph? codeGraph);
}