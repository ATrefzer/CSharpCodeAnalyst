using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Messages;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Areas.SearchArea;

public class SearchViewModel : INotifyPropertyChanged
{
    private readonly MessageBus _messaging;
    private readonly DispatcherTimer _searchTimer;
    private ObservableCollection<SearchItemViewModel> _allItems;
    private CodeGraph? _codeGraph;
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
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            ExecuteSearchInternal();
        };

        SearchCommand = new WpfCommand(ExecuteSearch);
        ClearSearchCommand = new WpfCommand(ClearSearch);
        AddSelectedToGraphCommand = new WpfCommand<object>(AddSelectedToGraph);
        AddSelectedToGraphCollapsedCommand = new WpfCommand<object>(AddSelectedToGraphCollapsed);
        PartitionCommand = new WpfCommand<SearchItemViewModel>(OnPartition, CanPartition);
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
    public ICommand AddSelectedToGraphCollapsedCommand { get; }
    public ICommand PartitionCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool CanPartition(SearchItemViewModel? vm)
    {
        return vm?.CodeElement is { ElementType: CodeElementType.Class };
    }

    private void OnPartition(SearchItemViewModel? vm)
    {
        if (vm?.CodeElement != null)
        {
            _messaging.Publish(new ShowPartitionsRequest(vm.CodeElement, false));
        }
    }

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

        var root = CreateSearchExpression();
        var filtered = AllItems.Where(item => root.Evaluate(item)).ToList();
        FilteredItems = new ObservableCollection<SearchItemViewModel>(filtered);
    }

    private IExpression CreateSearchExpression()
    {
        // Or binds less.
        var orTerms = SearchText
            .ToLowerInvariant()
            .Split(['|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var orExpressions = new List<IExpression>();
        foreach (var orTerm in orTerms)
        {
            var andExpressions = orTerm
                .Split([' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(IExpression (t) => new Term(t))
                .ToArray();

            orExpressions.Add(new And(andExpressions));
        }


        if (orExpressions.Count == 1)
        {
            return orExpressions[0];
        }

        var root = new Or(orExpressions.ToArray());
        return root;
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
        ExecuteSearchInternal(); // Immediately show all items
    }

    private void AddSelectedToGraph(object? selectedItems)
    {
        AddSelectedToGraphInternal(selectedItems, false);
    }

    private void AddSelectedToGraphCollapsed(object? selectedItems)
    {
        AddSelectedToGraphInternal(selectedItems, true);
    }

    private void AddSelectedToGraphInternal(object? selectedItems, bool addCollapsed)
    {
        if (selectedItems is IList list)
        {
            var codeElements = list.Cast<SearchItemViewModel>()
                .Where(item => item.CodeElement != null)
                .Select(item => item.CodeElement!)
                .ToList();

            if (codeElements.Count > 0)
            {
                _messaging.Publish(new AddNodeToGraphRequest(codeElements, addCollapsed));
            }
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}