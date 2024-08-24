using Contracts.Graph;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;

namespace CSharpCodeAnalyst.GraphArea.Highlighig
{
    interface IHighlighting
    {
        /// <summary>
        /// Clear any internal state before another highlighting is selected-
        /// </summary>
        /// <param name="graphViewer"></param>
        void Clear(GraphViewer? graphViewer);
        void Highlight(GraphViewer? graphViewer, IViewerObject? viewerObject, CodeGraph? codeGraph);
    }
    abstract class HighlightingBase : IHighlighting
    {
        public abstract void Highlight(GraphViewer? graphViewer, IViewerObject? viewerObject, CodeGraph? codeGraph);

        Color NormalColor = Color.Black;
        Color HighlightColor = Color.Red;
        int HighlightWeight = 3;
        int NormalWeight = 1;

        protected void Highlight(IViewerEdge edge)
        {
            edge.Edge.Attr.Color = HighlightColor;
            edge.Edge.Attr.LineWidth = HighlightWeight;
        }

        protected void ClearHighlight(IViewerEdge? edge)
        {
          
            edge.Edge.Attr.Color = NormalColor;
            edge.Edge.Attr.LineWidth = NormalWeight;
        }

        protected void ClearAllEdges(GraphViewer? graphViewer)
        {
            if (graphViewer is null)
                return;
            var edges = graphViewer.Entities.OfType<IViewerEdge>();
            foreach (var edge in edges)
            {
                ClearHighlight(edge);
            }
        }

        public abstract void Clear(GraphViewer? graphViewer);
        
    }
}