using System.ComponentModel;
using System.Diagnostics;
using Contracts.Graph;

namespace CSharpCodeAnalyst.SearchArea;

[DebuggerDisplay("{Type} {Name} - {FullPath}")]
public class SearchItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? FullPath { get; set; }
    public CodeElement? CodeElement { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}