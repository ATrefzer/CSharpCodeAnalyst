using System.ComponentModel;
using System.Diagnostics;
using Contracts.Graph;

namespace CSharpCodeAnalyst.SearchArea;

[DebuggerDisplay("{Type} {Name} - {FullPath}")]
public class SearchItemViewModel : INotifyPropertyChanged
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? FullPath { get; set; }
    public CodeElement? CodeElement { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}