using System.Collections;
using System.Windows.Input;

namespace CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;

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
}

