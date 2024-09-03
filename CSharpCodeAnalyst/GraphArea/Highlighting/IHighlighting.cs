using Contracts.Graph;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;

namespace CSharpCodeAnalyst.GraphArea.Highlighting;

internal interface IHighlighting
{
    /// <summary>
    ///     Clear any internal state before another highlighting is selected-
    /// </summary>
    /// <param name="graphViewer"></param>
    void Clear(GraphViewer? graphViewer);

    void Highlight(GraphViewer? graphViewer, IViewerObject? viewerObject, CodeGraph? codeGraph);
}