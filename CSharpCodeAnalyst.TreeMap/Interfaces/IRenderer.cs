using System.Windows;
using System.Windows.Media;
using CSharpCodeAnalyst.Contracts;
using CSharpCodeAnalyst.TreeMap.Tools;

namespace CSharpCodeAnalyst.TreeMap.Interfaces
{
    public interface IRenderer
    {
        void RenderToDrawingContext(double actualWidth, double actualHeight, DrawingContext dc);

        void LoadData(IHierarchicalData zoomLevel);
        Point Transform(Point mousePosition);

        /// <summary>
        ///     Returns the node whose rectangle contains <paramref name="pos" />, or null. Only
        ///     meaningful after the control has been rendered at least once (the layout is computed
        ///     during rendering).
        /// </summary>
        IHierarchicalData? Hit(IHierarchicalData root, Point pos);

        IHighlighting? Highlighting { get; set; }
    }
}