using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Features.Graph.Highlighting;

internal interface IHighlighting
{
    void Highlight(IGraphViewerHighlighting graphViewer, IViewerObject? viewerObject,
        CodeGraph.Graph.CodeGraph? codeGraph);
}