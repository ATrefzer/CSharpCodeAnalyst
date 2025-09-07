using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;

namespace CSharpCodeAnalyst.SearchArea;

[DebuggerDisplay("{Type} {Name} - {FullPath}")]
public class SearchItemViewModel : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public CodeElement? CodeElement { get; set; }
    
    public BitmapImage? Icon => CodeElement != null ? CodeElementIconMapper.GetIcon(CodeElement.ElementType) : null;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}