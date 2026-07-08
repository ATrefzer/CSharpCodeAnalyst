using System.Windows;
using System.Windows.Media;
using CSharpCodeAnalyst.TreeMap.Tools;

namespace CSharpCodeAnalyst.TreeMap.Interfaces
{
    public interface IRenderer
    {
        void RenderToDrawingContext(double actualWidth, double actualHeight, DrawingContext dc);
    
        void LoadData(IHierarchicalData zoomLevel);
        Point Transform(Point mousePosition);

        IHighlighting? Highlighting { get; set; }
    }
}