using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Shared.Messages;

namespace CSharpCodeAnalyst.Features.AdvancedSearch;

[DebuggerDisplay("{Type} {Name} - {FullPath}")]
public sealed class SearchItemViewModel : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public CodeElement? CodeElement { get; set; }

    public bool IsSelected
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    public BitmapImage? Icon
    {
        get => CodeElement != null ? CodeElementIconMapper.GetIcon(CodeElement.ElementType) : null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}