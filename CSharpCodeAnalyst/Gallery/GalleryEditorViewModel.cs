using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CSharpCodeAnalyst.Areas.GraphArea;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Gallery;

public class GalleryEditorViewModel : INotifyPropertyChanged
{
    private readonly Func<string, GraphSession> _addItemAction;
    private readonly Action<GraphSession> _applySelectionAction;
    private readonly Action<GraphSession> _removeItemAction;
    private readonly Action<GraphSession> _selectItemAction;

    private string _newItemName = string.Empty;

    private GraphSession? _selectedItem;

    public GalleryEditorViewModel(Gallery gallery, Action<GraphSession> selectItemAction,
        Func<string, GraphSession> addItemAction, Action<GraphSession> removeItemAction,
        Action<GraphSession> applySelectionAction)
    {
        _selectItemAction = selectItemAction;
        _addItemAction = addItemAction;
        _removeItemAction = removeItemAction;
        _applySelectionAction = applySelectionAction;

        Items = new ObservableCollection<GraphSession>(gallery.Sessions);

        AddItemCommand = new WpfCommand(AddItem, CanAddItem);
        RemoveItemCommand = new WpfCommand<GraphSession>(RemoveItem);
        PreviewSelectedItemCommand = new WpfCommand<GraphSession>(SelectItem);
        LoadSelectedItemCommand = new WpfCommand(Apply, CanApply);
    }

    public GraphSession? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value!;
            LoadSelectedItemCommand.RaiseCanExecuteChanged();
        }
    }


    public WpfCommand LoadSelectedItemCommand { get; set; }

    public ObservableCollection<GraphSession> Items { get; }
    public WpfCommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand PreviewSelectedItemCommand { get; }

    public string NewItemName
    {
        get => _newItemName;
        set
        {
            _newItemName = value;
            AddItemCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(NewItemName));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Apply()
    {
        if (SelectedItem != null)
        {
            _applySelectionAction(SelectedItem);
        }
    }

    private bool CanApply()
    {
        return SelectedItem != null;
    }

    private void AddItem()
    {
        var state = _addItemAction(NewItemName);
        Items.Add(state);
        NewItemName = string.Empty;
    }

    private bool CanAddItem()
    {
        return !string.IsNullOrWhiteSpace(NewItemName.Trim());
    }

    private void RemoveItem(GraphSession item)
    {
        _removeItemAction(item);
        Items.Remove(item);
    }

    private void SelectItem(GraphSession item)
    {
        _selectItemAction(item);
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}