using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CSharpCodeAnalyst.Shared.Converters;

/// <summary>
///     Visible when the bound number is greater than or equal to the threshold passed as
///     <c>ConverterParameter</c> (parsed with the invariant culture), Collapsed otherwise. Used to hide a
///     control below a zoom level.
/// </summary>
public class MinValueToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var threshold = 0.0;
        if (parameter is string s)
        {
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out threshold);
        }

        var number = value switch
        {
            double d => d,
            int i => i,
            _ => double.NaN
        };

        return !double.IsNaN(number) && number >= threshold ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
