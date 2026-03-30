using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CSharpCodeAnalyst.Areas.GraphArea;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Gallery;

public sealed class GalleryEditorViewModel : INotifyPropertyChanged
{
    private readonly Func<string, GraphSession> _addAction;
    private readonly Action<GraphSession> _loadAction;
    private readonly Action<GraphSession> _removeAction;
    private readonly Action<GraphSession> _previewAction;

    private string _newItemName = string.Empty;

    private GraphSession? _selectedItem;

    public GalleryEditorViewModel(Gallery gallery, Action<GraphSession> previewAction,
        Func<string, GraphSession> addAction, Action<GraphSession> removeAction,
        Action<GraphSession> loadAction)
    {
        _previewAction = previewAction;
        _addAction = addAction;
        _removeAction = removeAction;
        _loadAction = loadAction;

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
            WpfCommand.RaiseCanExecuteChanged();
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
            WpfCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(NewItemName));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Apply()
    {
        if (SelectedItem != null)
        {
            _loadAction(SelectedItem);
        }
    }

    private bool CanApply()
    {
        return SelectedItem != null;
    }

    private void AddItem()
    {
        var state = _addAction(NewItemName);
        Items.Add(state);
        NewItemName = string.Empty;
    }

    private bool CanAddItem()
    {
        return !string.IsNullOrWhiteSpace(NewItemName.Trim());
    }

    private void RemoveItem(GraphSession item)
    {
        _removeAction(item);
        Items.Remove(item);
    }

    private void SelectItem(GraphSession item)
    {
        _previewAction(item);
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}