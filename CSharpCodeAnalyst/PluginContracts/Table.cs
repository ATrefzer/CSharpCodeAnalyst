using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using CSharpCodeAnalyst.Areas.TableArea;
using Prism.Commands;

namespace CSharpCodeAnalyst.PluginContracts;

/// <summary>
///     Main interface for table data.
///     Provides column definitions and data objects.
/// </summary>
public abstract class Table : INotifyPropertyChanged
{
    private string _title;

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

    public abstract IEnumerable<TableRow> GetData();

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
}

public class CommandDefinition
{
    public ICommand? Command { get; set; }
    public string? Header { get; set; }
}