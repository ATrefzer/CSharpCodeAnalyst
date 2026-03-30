using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Messages;

namespace CSharpCodeAnalyst.Areas.AdvancedSearchArea;

[DebuggerDisplay("{Type} {Name} - {FullPath}")]
public sealed class SearchItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public CodeElement? CodeElement { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
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