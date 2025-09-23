using System.Windows;

namespace CSharpCodeAnalyst.Areas.TableArea;

/// <summary>
///     Main interface for table data.
///     Provides column definitions and data objects.
/// </summary>
public interface ITableData
{
    IEnumerable<ITableColumnDefinition> GetColumns();

    IEnumerable<object> GetData();

    /// <summary>
    ///     Optional template for Row Details (can be null)
    /// </summary>
    DataTemplate? GetRowDetailsTemplate();

    /// <summary>
    ///     Title for the whole data.
    /// </summary>
    string? GetTitle();
}