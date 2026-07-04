using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Shared.Tabs;

/// <summary>
///     A tab created on demand for analyzer-style results (Method Complexity, Type Cohesion,
///     Partitions, ...), keyed by <see cref="Id" /> so publishing another result under the same id
///     updates the existing tab in place instead of creating a duplicate. Owned by an
///     <see cref="System.Collections.ObjectModel.ObservableCollection{T}" /> on MainViewModel;
///     MainWindow's code-behind projects that collection onto the working-area TabControl.
/// </summary>
public sealed class DynamicTabViewModel(string id, string title, Table table) : INotifyPropertyChanged
{
    public string Id { get; } = id;

    public string Title
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    } = title;

    public Table Table
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    } = table;

    /// <summary>Set by the owner (MainViewModel) right after construction.</summary>
    public ICommand? CloseCommand { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}