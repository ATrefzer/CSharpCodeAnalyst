using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CSharpCodeAnalyst.GraphArea;
using Prism.Commands;

namespace CSharpCodeAnalyst.Gallery;

public class GalleryEditorViewModel : INotifyPropertyChanged
{
    private readonly Func<string, GraphSessionState> _addItemAction;
    private readonly Action<GraphSessionState> _applySelectionAction;
    private readonly Gallery _gallery;
    private readonly Action<GraphSessionState> _removeItemAction;
    private readonly Action<GraphSessionState> _selectItemAction;

    private string _newItemName = string.Empty;

    private GraphSessionState? _selectedItem;

    public GalleryEditorViewModel(Gallery gallery, Action<GraphSessionState> selectItemAction,
        Func<string, GraphSessionState> addItemAction, Action<GraphSessionState> removeItemAction,
        Action<GraphSessionState> applySelectionAction)
    {
        _gallery = gallery;
        _selectItemAction = selectItemAction;
        _addItemAction = addItemAction;
        _removeItemAction = removeItemAction;
        _applySelectionAction = applySelectionAction;


        Items = new ObservableCollection<GraphSessionState>(gallery.Sessions);

        AddItemCommand = new DelegateCommand(AddItem, CanAddItem);
        RemoveItemCommand = new DelegateCommand<GraphSessionState>(RemoveItem);
        PreviewSelectedItemCommand = new DelegateCommand<GraphSessionState>(SelectItem);
        LoadSelectedItemCommand = new DelegateCommand(Apply, CanApply);
    }

    public GraphSessionState? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value!;
            LoadSelectedItemCommand.RaiseCanExecuteChanged();
        }
    }


    public DelegateCommand LoadSelectedItemCommand { get; set; }

    public ObservableCollection<GraphSessionState> Items { get; }
    public DelegateCommand AddItemCommand { get; }
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

    private void RemoveItem(GraphSessionState item)
    {
        _removeItemAction(item);
        Items.Remove(item);
    }

    private void SelectItem(GraphSessionState item)
    {
        _selectItemAction(item);
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}