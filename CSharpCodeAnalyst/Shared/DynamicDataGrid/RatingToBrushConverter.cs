using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Shared.DynamicDataGrid;

/// <summary>
///     Maps a metric cell value to a soft background brush using the <see cref="IMetricRating" />
///     passed as the converter parameter. UI concern - the colour mapping lives here, the rating
///     thresholds live with the metric in the AnalyzerSdk.
/// </summary>
public sealed class RatingToBrushConverter : IValueConverter
{
    public static readonly RatingToBrushConverter Instance = new();

    // Soft, pastel backgrounds so the (dark) cell text stays readable.
    private static readonly Brush Good = Freeze(Color.FromRgb(0xE6, 0xF4, 0xEA));
    private static readonly Brush Warning = Freeze(Color.FromRgb(0xFF, 0xF4, 0xE5));
    private static readonly Brush Bad = Freeze(Color.FromRgb(0xFC, 0xE8, 0xE6));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not IMetricRating rating || value is not IConvertible)
        {
            return Brushes.Transparent;
        }

        double numeric;
        try
        {
            numeric = System.Convert.ToDouble(value, culture);
        }
        catch (Exception)
        {
            return Brushes.Transparent;
        }

        return rating.Evaluate(numeric) switch
        {
            RatingLevel.Good => Good,
            RatingLevel.Warning => Warning,
            RatingLevel.Bad => Bad,
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
