
using System.Windows;
using System.Windows.Media;
using CSharpCodeAnalyst.TreeMap.Common;
using CSharpCodeAnalyst.TreeMap.Drawing;
using CSharpCodeAnalyst.TreeMap.Interfaces;
using CSharpCodeAnalyst.TreeMap.Tools;

namespace CSharpCodeAnalyst.TreeMap.TreeMap
{
    public sealed class SquarifiedTreeMapRenderer : IRenderer
    {
        private IHierarchicalData? _data;

        // ReSharper disable once NotAccessedField.Local
        private int _level = -1;
        private IBrushFactory _brushFactory;

        public SquarifiedTreeMapRenderer(IBrushFactory brushFactory)
        {
            _brushFactory = brushFactory;
        }


        public IHighlighting? Highlighting { get; set; }

        /// <summary>
        /// Ensure that SumAreaMetrics and NormalizeWeightMetric was called and
        /// no node has an area of 0.
        /// </summary>
        public void LoadData(IHierarchicalData data)
        {
            _data = data;
        }

        public void RenderToDrawingContext(double actualWidth, double actualHeight, DrawingContext dc)
        {
            if (_data == null)
            {
                return;
            }

            // Calculate the layout
            var layout = new SquarifiedTreeMapLayout();
            layout.Layout(_data, actualWidth, actualHeight);

            // Render to drawing context
            _level = 0;
            RenderToDrawingContext(dc, _data);
        }

        public Point Transform(Point mousePosition)
        {
            // We directly daw in screen coordinates.
            return mousePosition;
        }

        private static RectangularLayoutInfo GetLayout(IHierarchicalData data)
        {
            return data.Layout as RectangularLayoutInfo;
        }

        private SolidColorBrush GetBrush(IHierarchicalData data)
        {
            if (Highlighting != null && Highlighting.IsHighlighted(data))
            {
                return DefaultDrawingPrimitives.HighlightBrush;
            }

            SolidColorBrush brush;
            if (data.ColorKey != null)
            {
                brush = _brushFactory.GetBrush(data.ColorKey);
            }
            else
            {
                // For non leaf nodes the weight is 0. We only can merge area metrics.
                // See HierarchyBuilder.InsertLeaf.

                var color = DefaultDrawingPrimitives.WhiteToRedGradient.GradientStops.GetRelativeColor(data.NormalizedWeightMetric);
                brush = new SolidColorBrush(color);
                brush.Freeze();
            }

            return brush;
        }


        private void RenderToDrawingContext(DrawingContext dc, IHierarchicalData data)
        {
            _level++;
            if (data.IsLeafNode)
            {
                var brush = GetBrush(data);

                //dc.DrawRectangle(_gradient, _pen, data.Layout.Rect);
                var layout = GetLayout(data);
                dc.DrawRectangle(brush, DefaultDrawingPrimitives.BlackPen, layout.Rect);
            }

            foreach (var child in data.Children)
            {
                RenderToDrawingContext(dc, child);
            }

            _level--;
        }
    }
}