using Contracts.Graph;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea.Highlighting;

internal interface IHighlighting
{
   void Highlight(IGraphViewerHighlighting graphViewer, IViewerObject? viewerObject,
        CodeGraph? codeGraph);
}