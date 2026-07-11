using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Shared.Messages;

namespace CSharpCodeAnalyst.Features.Graph;

/// <summary>
///     Backs <see cref="AddChildrenDialog" />: a simple multi-select list of the direct children
///     of a type, each shown with its tree-view icon and its plain name
/// </summary>
public sealed class AddChildrenDialogViewModel
{
    public AddChildrenDialogViewModel(IEnumerable<CodeElement> children)
    {
        Items = new ObservableCollection<ChildItemViewModel>(
            children.Select(child => new ChildItemViewModel(child)));
    }

    public ObservableCollection<ChildItemViewModel> Items { get; }

    public IReadOnlyList<CodeElement> SelectedElements
    {
        get => Items.Where(item => item.IsSelected).Select(item => item.Element).ToList();
    }
}

/// <summary>
///     One row in the add-children dialog.
/// </summary>
public sealed class ChildItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public ChildItemViewModel(CodeElement element)
    {
        Element = element;
        Name = element.Name;
        Icon = CodeElementIconMapper.GetIcon(element.ElementType);
    }

    public CodeElement Element { get; }
    public string Name { get; }
    public BitmapImage Icon { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}