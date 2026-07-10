using System.Windows;
using System.Windows.Media;
using CSharpCodeAnalyst.Contracts;
using CSharpCodeAnalyst.TreeMap.Common;
using CSharpCodeAnalyst.TreeMap.Drawing;
using CSharpCodeAnalyst.TreeMap.Interfaces;
using CSharpCodeAnalyst.TreeMap.Tools;

namespace CSharpCodeAnalyst.TreeMap.TreeMap;

public sealed class TreeMapRenderer : IRenderer
{
    private readonly IBrushFactory? _brushFactory;
    private readonly HitTest _hitTest = new();
    private IHierarchicalData? _data;
    private TreeMapLayout? _layoutMap;

    // ReSharper disable once NotAccessedField.Local
    private int _level = -1;

    public TreeMapRenderer(IBrushFactory? brushFactory)
    {
        _brushFactory = brushFactory;
    }

    public IHighlighting? Highlighting { get; set; }

    /// <summary>
    ///     Ensure that SumAreaMetrics and NormalizeWeightMetric was called and
    ///     no node has an area of 0.
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

        // Calculate the layout. Keep the resulting map so hit testing can reuse it.
        var layout = new SquarifiedTreeMapLayout();
        _layoutMap = layout.Layout(_data, actualWidth, actualHeight);

        // Render to drawing context
        _level = 0;
        RenderToDrawingContext(dc, _data);
    }

    public Point Transform(Point mousePosition)
    {
        // We directly daw in screen coordinates.
        return mousePosition;
    }

    public IHierarchicalData? Hit(IHierarchicalData root, Point pos)
    {
        return _layoutMap == null ? null : _hitTest.Hit(root, pos, _layoutMap);
    }

    private RectangularLayoutInfo GetLayout(IHierarchicalData data)
    {
        return _layoutMap?.Get(data) ?? new RectangularLayoutInfo();
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
            if (_brushFactory is null)
            {
                throw new InvalidOperationException("No BrushFactory has been set.");
            }
            brush = _brushFactory.GetBrush(data.ColorKey);
        }
        else
        {
            // For non leaf nodes the weight is 0. We only can merge area metrics.
            // See HierarchyBuilder.InsertLeaf.

            var color = DefaultDrawingPrimitives.WhiteToRedGradient.GradientStops.GetRelativeColor(data.NormalizedWeightMetric);
            brush = BrushCache.GetBrush(color);
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