using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;

namespace CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;

/// <summary>
///     Main interface for table data.
///     Provides column definitions and data objects.
/// </summary>
public abstract class Table : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public abstract IEnumerable<TableColumnDefinition> GetColumns();

    public abstract ObservableCollection<TableRow> GetData();

    /// <summary>
    ///     Optional template for Row Details (can be null)
    /// </summary>
    public abstract DataTemplate? GetRowDetailsTemplate();

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public virtual List<CommandDefinition> GetCommands()
    {
        return [];
    }

    /// <summary>
    ///     Whether this table offers a search box in the <c>DynamicDataGrid</c>. Only tables that
    ///     override this to return true get the box; it stays collapsed otherwise.
    /// </summary>
    public virtual bool CanFilter => false;

    /// <summary>
    ///     Returns the rows matching <paramref name="searchText" />. The table decides which
    ///     column(s) the text is matched against and what search syntax it understands. An empty
    ///     search text returns all rows. Only called when <see cref="CanFilter" /> is true.
    /// </summary>
    public virtual ObservableCollection<TableRow> Filter(string searchText)
    {
        return GetData();
    }

    protected static DataTemplate? CreateDataTemplateFromString(string xamlTemplate)
    {
        try
        {
            return (DataTemplate)XamlReader.Parse(xamlTemplate);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating row details template: {ex.Message}");
            return null;
        }
    }
}

public class CommandDefinition
{
    public ICommand? Command { get; set; }
    public string? Header { get; set; }
}