using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Areas.GraphArea.Filtering;

public sealed class GraphHideDialogViewModel : INotifyPropertyChanged
{

    public GraphHideDialogViewModel(GraphHideFilter currentFilter)
    {
        Filter = currentFilter.Clone();

        // Build element type checkboxes dynamically
        ElementTypeOptions = new ObservableCollection<CheckableItem<CodeElementType>>(
            GraphHideFilter.HideableElementTypes
                .OrderBy(t => t.ToString())
                .Select(type => new CheckableItem<CodeElementType>(
                    type,
                    FormatCodeElementType(type),
                    Filter.HiddenElementTypes.Contains(type)
                ))
        );

        // Build relationship type checkboxes dynamically
        RelationshipTypeOptions = new ObservableCollection<CheckableItem<RelationshipType>>(
            GraphHideFilter.HideableRelationshipTypes
                .OrderBy(t => t.ToString())
                .Select(type => new CheckableItem<RelationshipType>(
                    type,
                    FormatRelationshipType(type),
                    Filter.HiddenRelationshipTypes.Contains(type)
                ))
        );

        ApplyCommand = new WpfCommand(Apply);
        ResetCommand = new WpfCommand(Reset);
    }

    public ObservableCollection<CheckableItem<CodeElementType>> ElementTypeOptions { get; }
    public ObservableCollection<CheckableItem<RelationshipType>> RelationshipTypeOptions { get; }

    public ICommand ApplyCommand { get; }
    public ICommand ResetCommand { get; }

    public GraphHideFilter Filter { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Apply()
    {
        // Update filter based on checkbox selections
        Filter.HiddenElementTypes.Clear();
        foreach (var item in ElementTypeOptions.Where(i => i.IsChecked))
        {
            Filter.HiddenElementTypes.Add(item.Value);
        }

        Filter.HiddenRelationshipTypes.Clear();
        foreach (var item in RelationshipTypeOptions.Where(i => i.IsChecked))
        {
            Filter.HiddenRelationshipTypes.Add(item.Value);
        }
    }

    public void Reset()
    {
        // Uncheck all items
        foreach (var item in ElementTypeOptions)
        {
            item.IsChecked = false;
        }

        foreach (var item in RelationshipTypeOptions)
        {
            item.IsChecked = false;
        }

        Filter.Clear();
    }

    private static string FormatCodeElementType(CodeElementType type)
    {
        return type.ToString();
    }

    private static string FormatRelationshipType(RelationshipType type)
    {
        return type.ToString();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
///     Helper class for checkbox binding in the UI.
/// </summary>
public sealed class CheckableItem<T> : INotifyPropertyChanged where T : struct, Enum
{
    private bool _isChecked;

    public CheckableItem(T value, string displayName, bool isChecked)
    {
        Value = value;
        DisplayName = displayName;
        _isChecked = isChecked;
    }

    public T Value { get; }
    public string DisplayName { get; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked != value)
            {
                _isChecked = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}