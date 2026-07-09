using System.Windows.Media;
using CSharpCodeAnalyst.TreeMap.Common;
using CSharpCodeAnalyst.TreeMap.Interfaces;

namespace CSharpCodeAnalyst.Features.History;

internal class BrushFactory : IBrushFactory
{
    private readonly Dictionary<string, SolidColorBrush> _brushes = new();

    public BrushFactory(List<string> names)
    {
        var sortedNames = names
            .Distinct()
            .OrderBy(n => n, StringComparer.InvariantCultureIgnoreCase)
            .ToList();

        var colors = DistinctColorPalette.GetColors(sortedNames.Count());
        for (var i = 0; i < sortedNames.Count(); i++)
        {
            var brush = new SolidColorBrush(colors[i]);
            brush.Freeze();

            _brushes[sortedNames[i]] = brush;
        }
    }

    public SolidColorBrush GetBrush(string name)
    {
        // Files without a known main developer carry an empty color key (and are deliberately
        // not in the map). Fall back to the neutral default instead of throwing.
        return _brushes.TryGetValue(name, out var brush) ? brush : DefaultDrawingPrimitives.DefaultBrush;
    }
}