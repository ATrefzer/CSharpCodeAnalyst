using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Resources;
using Prism.Commands;

namespace CSharpCodeAnalyst.SearchArea;

public class SearchViewModel : INotifyPropertyChanged
{
    private readonly MessageBus _messaging;
    private readonly DispatcherTimer _searchTimer;
    private CodeGraph? _codeGraph;
    private ObservableCollection<SearchItemViewModel> _allItems;
    private ObservableCollection<SearchItemViewModel> _filteredItems;
    private string _searchText;

    public SearchViewModel(MessageBus messaging)
    {
        _messaging = messaging;
        _searchText = string.Empty;
        _allItems = [];
        _filteredItems = [];

        // Initialize debounce timer for search
        _searchTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300) // 300ms debounce
        };
        _searchTimer.Tick += (s, e) =>
        {
            _searchTimer.Stop();
            ExecuteSearchInternal();
        };

        SearchCommand = new DelegateCommand(ExecuteSearch);
        ClearSearchCommand = new DelegateCommand(ClearSearch);
        AddSelectedToGraphCommand = new DelegateCommand<object>(AddSelectedToGraph);
    }

    public ObservableCollection<SearchItemViewModel> AllItems
    {
        get => _allItems;
        set
        {
            _allItems = value;
            OnPropertyChanged(nameof(AllItems));
        }
    }

    public ObservableCollection<SearchItemViewModel> FilteredItems
    {
        get => _filteredItems;
        set
        {
            _filteredItems = value;
            OnPropertyChanged(nameof(FilteredItems));
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
            // Execute search with debouncing
            _searchTimer.Stop();
            _searchTimer.Start();
        }
    }

    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand AddSelectedToGraphCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void LoadCodeGraph(CodeGraph codeGraph)
    {
        _codeGraph = codeGraph;
        BuildFlatList();
        ExecuteSearch(); // Initial filtering
    }

    private void BuildFlatList()
    {
        AllItems.Clear();

        if (_codeGraph == null)
            return;

        var items = new List<SearchItemViewModel>();
        
        foreach (var node in _codeGraph.Nodes.Values.OrderBy(n => n.FullName))
        {
            items.Add(new SearchItemViewModel
            {
                Name = node.Name,
                Type = node.ElementType.ToString(),
                FullPath = node.FullName,
                CodeElement = node
            });
        }

        AllItems = new ObservableCollection<SearchItemViewModel>(items);
    }

    private void ExecuteSearch()
    {
        // Stop debounce timer and execute immediately
        _searchTimer.Stop();
        ExecuteSearchInternal();
    }

    private void ExecuteSearchInternal()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredItems = new ObservableCollection<SearchItemViewModel>(AllItems);
            return;
        }

        var searchTerms = SearchText.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var filtered = AllItems.Where(item => 
        {
            var name = item.Name?.ToLowerInvariant() ?? string.Empty;
            var type = item.Type?.ToLowerInvariant() ?? string.Empty;
            var fullPath = item.FullPath?.ToLowerInvariant() ?? string.Empty;
            
            // All search terms must match somewhere in the item
            return searchTerms.All(term => 
                name.Contains(term) || 
                type.Contains(term) || 
                fullPath.Contains(term));
        }).ToList();

        FilteredItems = new ObservableCollection<SearchItemViewModel>(filtered);
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
        ExecuteSearchInternal(); // Immediately show all items
    }

    private void AddSelectedToGraph(object? selectedItems)
    {
        if (selectedItems is System.Collections.IList list)
        {
            var codeElements = list.Cast<SearchItemViewModel>()
                .Where(item => item.CodeElement != null)
                .Select(item => item.CodeElement!)
                .ToList();

            if (codeElements.Count > 0)
            {
                _messaging.Publish(new AddNodeToGraphRequest(codeElements));
            }
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}