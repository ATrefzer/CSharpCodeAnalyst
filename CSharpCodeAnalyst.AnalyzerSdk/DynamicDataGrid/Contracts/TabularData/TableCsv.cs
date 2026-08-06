using System.Globalization;
using System.Text;

namespace CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;

/// <summary>
///     Turns a table into CSV, so a result can be counted, sorted or pivoted somewhere else. Separate from
///     the grid that offers it, because the interesting part - quoting, and reading a cell the way the
///     column binds it - is worth testing on its own.
/// </summary>
public static class TableCsv
{
    /// <param name="columns">The columns, in display order. Their <c>PropertyName</c> reads the cell.</param>
    /// <param name="rows">
    ///     The rows to write. The caller passes what is on screen - filtered and sorted - rather than the
    ///     table's full data, because narrowing the result down is usually the first step.
    /// </param>
    /// <param name="separator">
    ///     Null takes the list separator of the current culture, which is what a spreadsheet on the same
    ///     machine expects: a comma in en-US, a semicolon in de-DE. With the wrong one the paste lands in a
    ///     single column.
    /// </param>
    public static string Build(IEnumerable<TableColumnDefinition> columns, IEnumerable<object> rows,
        string? separator = null)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        separator ??= CultureInfo.CurrentCulture.TextInfo.ListSeparator;

        var columnList = columns.ToList();
        var csv = new StringBuilder();

        csv.AppendLine(string.Join(separator, columnList.Select(column => Escape(column.Header, separator))));

        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(separator,
                columnList.Select(column => Escape(ReadCell(row, column.PropertyName), separator))));
        }

        return csv.ToString();
    }

    /// <summary>
    ///     Reads the property the column binds to. A column without a property name - a button, an image -
    ///     contributes an empty cell instead of breaking the row.
    /// </summary>
    private static string ReadCell(object row, string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return string.Empty;
        }

        var value = row.GetType().GetProperty(propertyName)?.GetValue(row);

        return value switch
        {
            null => string.Empty,

            // The same culture the cell was rendered with, so a number reads the same in both places.
            IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    /// <summary>
    ///     RFC 4180: quote a value that holds the separator, a quote or a line break, and double the quotes
    ///     inside it. Names and hints in this application contain all three.
    /// </summary>
    private static string Escape(string? value, string separator)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains(separator, StringComparison.Ordinal) && !value.Contains('"') &&
            !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
