using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using Contracts.Graph;
using CSharpCodeAnalyst.Messages;

namespace CSharpCodeAnalyst.Areas.TreeArea;

[DebuggerDisplay("{Type} {Name}")]
public class TreeItemViewModel : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isHighlighted;
    private bool _isVisible = true;

    public string? Name { get; set; }
    public string? Type { get; set; }
    public CodeElement? CodeElement { get; set; }
    public ObservableCollection<TreeItemViewModel> Children { get; set; } = [];

    public BitmapImage? Icon
    {
        get
        {
            // Virtual root node for "External" uses namespace icon
            if (CodeElement == null && Type == "Virtual Root")
            {
                return CodeElementIconMapper.GetIcon(CodeElementType.Namespace);
            }
            return CodeElement != null ? CodeElementIconMapper.GetIcon(CodeElement.ElementType) : null;
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            OnPropertyChanged(nameof(IsExpanded));
        }
    }

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            _isHighlighted = value;
            OnPropertyChanged(nameof(IsHighlighted));
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            OnPropertyChanged(nameof(IsVisible));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TreeItemViewModel Clone(bool resetTreeItemsStates)
    {
        var clone = new TreeItemViewModel
        {
            _isExpanded = false,
            _isHighlighted = false,
            _isVisible = true
        };

        if (!resetTreeItemsStates)
        {
            clone._isExpanded = IsExpanded;
            clone._isVisible = IsVisible;
            clone._isHighlighted = IsHighlighted;
        }

        clone.CodeElement = CodeElement;
        clone.Name = Name;
        clone.Type = Type;

        clone.Children =
            new ObservableCollection<TreeItemViewModel>(Children.Select(c => c.Clone(resetTreeItemsStates)));
        return clone;
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    internal bool CanShowContextMenuForItem()
    {
        return CodeElement?.ElementType is CodeElementType.Method or
            CodeElementType.Assembly or
            CodeElementType.Namespace or
            CodeElementType.Class or
            CodeElementType.Interface or
            CodeElementType.Struct or
            CodeElementType.Enum or
            CodeElementType.Field or
            CodeElementType.Property or
            CodeElementType.Event or
            CodeElementType.Delegate;
    }
}