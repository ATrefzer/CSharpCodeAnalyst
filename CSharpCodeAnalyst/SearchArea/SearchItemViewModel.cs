using System.ComponentModel;
using System.Diagnostics;
using Contracts.Graph;

namespace CSharpCodeAnalyst.SearchArea;

[DebuggerDisplay("{Type} {Name} - {FullPath}")]
public class SearchItemViewModel : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public CodeElement? CodeElement { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}