using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Messages;
using CSharpCodeAnalyst.Refactoring;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Areas.AdvancedSearchArea;

public sealed class AdvancedSearchViewModel : INotifyPropertyChanged
{
    private readonly MessageBus _messaging;
    private readonly RefactoringService _refactoringService;
    private readonly DispatcherTimer _searchTimer;
    private ObservableCollection<SearchItemViewModel> _allItems;
    private CodeGraph.Graph.CodeGraph? _codeGraph;
    private ObservableCollection<SearchItemViewModel> _filteredItems;
    private string _searchText;

    public AdvancedSearchViewModel(MessageBus messaging, RefactoringService refactoringService)
    {
        _messaging = messaging;
        _refactoringService = refactoringService;
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

        ClearSearchCommand = new WpfCommand(ClearSearch);
        AddSelectedToGraphCommand = new WpfCommand(AddSelectedToGraph);
        AddSelectedToGraphCollapsedCommand = new WpfCommand(AddSelectedToGraphCollapsed);
        PartitionCommand = new WpfCommand<SearchItemViewModel>(OnPartition, CanPartition);
        CopyToClipboardCommand = new WpfCommand<SearchItemViewModel>(OnCopyToClipboard);
        SelectAllCommand = new WpfCommand(SelectAll);
        DeselectAllCommand = new WpfCommand(DeselectAll);

        SetMovementTargetCommand = new WpfCommand<SearchItemViewModel>(RefactoringSetMovementTarget, RefactoringCanSetMovementTarget);
        MoveSelectedCommand = new WpfCommand(RefactoringMoveCodeElement, RefactoringCanMoveCodeElement);
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

    public ICommand ClearSearchCommand { get; }
    public ICommand AddSelectedToGraphCommand { get; }
    public ICommand AddSelectedToGraphCollapsedCommand { get; }
    public ICommand PartitionCommand { get; }
    public ICommand CopyToClipboardCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand SetMovementTargetCommand { get; }
    public ICommand MoveSelectedCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool RefactoringCanSetMovementTarget(SearchItemViewModel vm)
    {
        return _refactoringService.CanSetMovementTarget(vm.CodeElement?.Id);
    }

    private bool RefactoringCanMoveCodeElement()
    {
        var ids = GetSelectedCodeElements().Select(e => e.Id).ToHashSet();
        return _refactoringService.CanMoveCodeElements(ids);
    }

    private void RefactoringMoveCodeElement()
    {
        var elementIds = GetSelectedCodeElements().Select(e => e.Id).ToHashSet();
        if (elementIds.Any())
        {
            _refactoringService.MoveCodeElements(elementIds);
        }
    }

    public string GetRefactoringNewMoveParent()
    {
        var target = _refactoringService.GetMovementTarget();
        return target?.Name != null ? target.Name : string.Empty;
    }


    private void RefactoringSetMovementTarget(SearchItemViewModel vm)
    {
        _refactoringService.SetMovementTarget(vm.CodeElement?.Id);
    }

    private void OnCopyToClipboard(SearchItemViewModel item)
    {
        var text = item.CodeElement?.FullName;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Clipboard.SetText(text);
    }

    private static bool CanPartition(SearchItemViewModel? vm)
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

    public void HandleCodeGraphRefactored(CodeGraphRefactored message)
    {
        // This operation is not that expensive
        LoadCodeGraph(message.Graph);
    }


    public void LoadCodeGraph(CodeGraph.Graph.CodeGraph codeGraph)
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

        var root = SearchExpressionFactory.CreateSearchExpression(SearchText);
        var filtered = AllItems.Where(item => root.Evaluate(item.CodeElement!)).ToList();
        FilteredItems = new ObservableCollection<SearchItemViewModel>(filtered);
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
        ExecuteSearchInternal(); // Immediately show all items
    }

    private void SelectAll()
    {
        foreach (var item in FilteredItems)
        {
            item.IsSelected = true;
        }
    }

    private void DeselectAll()
    {
        foreach (var item in FilteredItems)
        {
            item.IsSelected = false;
        }
    }

    private void AddSelectedToGraph()
    {
        AddSelectedToGraphInternal(false);
    }

    private void AddSelectedToGraphCollapsed()
    {
        AddSelectedToGraphInternal(true);
    }

    private void AddSelectedToGraphInternal(bool addCollapsed)
    {
        var codeElements = GetSelectedCodeElements();

        if (codeElements.Count > 0)
        {
            _messaging.Publish(new AddNodeToGraphRequest(codeElements, addCollapsed));
        }
    }

    /// <summary>
    ///     Gets selected code elements from all items, including the
    ///     currently non-visible.
    /// </summary>
    private List<CodeElement> GetSelectedCodeElements()
    {
        return AllItems
            .Where(item => item is { IsSelected: true, CodeElement: not null })
            .Select(item => item.CodeElement!)
            .ToList();
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}