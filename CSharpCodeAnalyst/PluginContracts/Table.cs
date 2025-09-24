using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;

namespace CSharpCodeAnalyst.PluginContracts;

/// <summary>
///     Main interface for table data.
///     Provides column definitions and data objects.
/// </summary>
public abstract class Table : INotifyPropertyChanged
{
    private string _title = string.Empty;

    /// <summary>
    ///     Title for the whole data.
    /// </summary>
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged();
        }
    }


    public event PropertyChangedEventHandler? PropertyChanged;
    public abstract IEnumerable<TableColumnDefinition> GetColumns();

    public abstract ObservableCollection<TableRow> GetData();

    /// <summary>
    ///     Optional template for Row Details (can be null)
    /// </summary>
    public abstract DataTemplate? GetRowDetailsTemplate();

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public virtual List<CommandDefinition> GetCommands()
    {
        return [];
    }

    protected DataTemplate? CreateDataTemplateFromString(string xamlTemplate)
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