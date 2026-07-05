using System.Windows.Input;

namespace CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;

public class TableColumnDefinition
{
    public string PropertyName { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public ColumnType Type { get; set; } = ColumnType.Text;

    /// <summary>
    ///     Width of column (0 = Auto)
    /// </summary>
    public double Width { get; set; } = 0;

    public ICommand? ClickCommand { get; set; }
    public object? CommandParameter { get; set; }
    public bool IsExpandable { get; set; }

    public string? SortMemberName { get; set; } = null;

    /// <summary>
    ///     Optional metric rating. When set, the grid colors the cell background according to the
    ///     rated value (green / orange / red). Null = no rating, cell renders normally.
    /// </summary>
    public IMetricRating? Rating { get; set; }

    /// <summary>
    ///     Which property the <see cref="Rating" /> is evaluated against. Defaults to
    ///     <see cref="PropertyName" />. Set this when the displayed column is a formatted string but
    ///     the rating should use the underlying numeric value (mirrors <see cref="SortMemberName" />).
    /// </summary>
    public string? RatingValuePropertyName { get; set; } = null;
}

