using Contracts.Graph;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea.Highlighting;

internal interface IHighlighting
{
    /// <summary>
    ///     Clear any internal state before another highlighting is selected-
    /// </summary>
    /// <param name="graphViewer"></param>
    void Clear(Microsoft.Msagl.WpfGraphControl.GraphViewer? graphViewer);

    void Highlight(Microsoft.Msagl.WpfGraphControl.GraphViewer? graphViewer, IViewerObject? viewerObject,
        CodeGraph? codeGraph);
}