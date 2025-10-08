using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Contracts.Graph;
using CSharpCodeAnalyst.Refactoring;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Messages;
using CSharpCodeAnalyst.Refactoring;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Areas.TreeArea;

public class TreeViewModel : INotifyPropertyChanged
{
    // For faster search
    private static readonly Dictionary<string, TreeItemViewModel> CodeElementIdToViewModel = new();
    private readonly Matcher _matcher;
    private readonly MessageBus _messaging;
    private CodeGraph? _codeGraph;
    private ObservableCollection<TreeItemViewModel> _filteredTreeItems;
    private string _searchText;
    private ObservableCollection<TreeItemViewModel> _treeItems;

    public TreeViewModel(MessageBus messaging)
    {
        _messaging = messaging;
        _searchText = string.Empty;
        _matcher = new Matcher();

        SearchCommand = new WpfCommand(ExecuteSearch);
        CollapseTreeCommand = new WpfCommand(CollapseTree);
        ClearSearchCommand = new WpfCommand(ClearSearch);
        AddNodeToGraphCommand = new WpfCommand<TreeItemViewModel>(AddNodeToGraph);
        PartitionTreeCommand = new WpfCommand<TreeItemViewModel>(Partition, CanPartition);
        PartitionWithBaseTreeCommand = new WpfCommand<TreeItemViewModel>(PartitionWithBase, CanPartition);
        CopyToClipboardCommand = new WpfCommand<TreeItemViewModel>(OnCopyToClipboard);
      
        // Refactoring
        DeleteFromModelCommand = new WpfCommand<TreeItemViewModel>(DeleteFromModel);
        CreateCodeElementCommand = new WpfCommand<TreeItemViewModel>(CreateCodeElement, CanCreateCodeElement);


        _filteredTreeItems = [];
        _treeItems = [];
    }

    private bool CanCreateCodeElement(TreeItemViewModel? tvm)
    {
        // null tvm means root level (empty space in tree) - this is allowed
        // Otherwise check if the CodeElement can have children
        return VirtualRefactoringService.CanCreateCodeElement(tvm?.CodeElement);
    }


    public ICommand CollapseTreeCommand { get; }

    public ICommand ClearSearchCommand { get; }


    public ObservableCollection<TreeItemViewModel> TreeItems
    {
        get => _treeItems;
        set
        {
            _treeItems = value;
            OnPropertyChanged(nameof(TreeItems));
        }
    }

    public ObservableCollection<TreeItemViewModel> FilteredTreeItems
    {
        get => _filteredTreeItems;
        set
        {
            _filteredTreeItems = value;
            OnPropertyChanged(nameof(FilteredTreeItems));
        }
    }


    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
        }
    }

    public ICommand SearchCommand { get; }
    public ICommand AddNodeToGraphCommand { get; private set; }
    public ICommand DeleteFromModelCommand { get; }
    public ICommand PartitionTreeCommand { get; private set; }
    public ICommand PartitionWithBaseTreeCommand { get; private set; }
    public ICommand CopyToClipboardCommand { get; private set; }
    public ICommand CreateCodeElementCommand { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static void OnCopyToClipboard(TreeItemViewModel vm)
    {
        var text = vm?.CodeElement?.FullName;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Clipboard.SetText(text);
    }

    private void PartitionWithBase(TreeItemViewModel vm)
    {
        if (vm.CodeElement is null)
        {
            return;
        }

        _messaging.Publish(new ShowPartitionsRequest(vm.CodeElement, true));
    }

    private static bool CanPartition(TreeItemViewModel? vm)
    {
        return vm is { CodeElement.ElementType: CodeElementType.Class };
    }

    private void Partition(TreeItemViewModel vm)
    {
        if (vm.CodeElement is null)
        {
            return;
        }

        _messaging.Publish(new ShowPartitionsRequest(vm.CodeElement, false));
    }

    private void DeleteFromModel(TreeItemViewModel obj)
    {
        var id = obj.CodeElement?.Id;
        if (id is null || _codeGraph is null)
        {
            return;
        }

        if (MessageBox.Show(Strings.DeleteFromModel_Message,
                Strings.Proceed_Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        _messaging.Publish(new DeleteFromModelRequest(id));
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
        ExecuteSearch();
    }

    private void CollapseTree()
    {
        CollapseTree(TreeItems);
    }

    private void AddNodeToGraph(TreeItemViewModel? item)
    {
        if (item is { CodeElement: not null })
        {
            // Forward to GraphViewModel. see MainViewModel.
            _messaging.Publish(new AddNodeToGraphRequest(item.CodeElement));
        }
    }

    /// <summary>
    /// Creates a code element at the root level (e.g., Assembly).
    /// Public method to be called from UI when right-clicking empty space.
    /// </summary>
    public void CreateCodeElementAtRoot()
    {
        CreateCodeElement(null);
    }

    private void CreateCodeElement(TreeItemViewModel? item)
    {
        if (_codeGraph == null)
        {
            return;
        }

        var refactoringService = new VirtualRefactoringService(_codeGraph);
        var parent = item?.CodeElement; // null means root level
        if (!VirtualRefactoringService.CanCreateCodeElement(parent))
        {
            return;
        }

        var viewModel = new CreateCodeElementDialogViewModel(refactoringService, parent);
        var dialog = new CreateCodeElementDialog(viewModel)
        {
            Owner = Application.Current.MainWindow
        };

        var result = dialog.ShowDialog();
        if (result == true && dialog.CreatedElement != null)
        {
            // Refresh the tree to show the new element
            var newTreeItem = CreateTreeViewItem(dialog.CreatedElement);

            if (parent == null)
            {
                // Add to root
                TreeItems.Add(newTreeItem);
            }
            else
            {
                // Find the parent in the tree and add as child
                if (CodeElementIdToViewModel.TryGetValue(parent.Id, out var parentViewModel))
                {
                    parentViewModel.Children.Add(newTreeItem);
                    parentViewModel.IsExpanded = true; // Expand to show the new item
                }
            }
        }
    }

    public void LoadCodeGraph(CodeGraph codeGraph)
    {
        _codeGraph = codeGraph;
        TreeItems.Clear();
        CodeElementIdToViewModel.Clear();

        SearchText = string.Empty;

        var rootNodes = codeGraph.Nodes.Values
            .Where(n => n.Parent == null)
            .ToList();

        // Separate internal and external root nodes
        var internalRoots = rootNodes.Where(n => !n.IsExternal).OrderBy(n => n.Name);
        var externalRoots = rootNodes.Where(n => n.IsExternal).OrderBy(n => n.Name);

        // Add internal roots directly
        foreach (var rootNode in internalRoots)
        {
            TreeItems.Add(CreateTreeViewItem(rootNode));
        }

        // If there are external elements, add them under an "External" virtual root
        if (externalRoots.Any())
        {
            var externalRootItem = new TreeItemViewModel
            {
                Name = "External",
                Type = "Virtual Root",
                CodeElement = null, // Virtual node - no actual CodeElement
                IsExpanded = false
            };

            foreach (var externalRoot in externalRoots)
            {
                externalRootItem.Children.Add(CreateTreeViewItem(externalRoot));
            }

            TreeItems.Add(externalRootItem);
        }
    }

    private static TreeItemViewModel CreateTreeViewItem(CodeElement element)
    {
        var item = new TreeItemViewModel
        {
            Name = element.Name,
            Type = element.ElementType.ToString(),
            CodeElement = element
        };
        CodeElementIdToViewModel.Add(element.Id, item);


        foreach (var child in element.Children.OrderBy(c => c.Name))
        {
            item.Children.Add(CreateTreeViewItem(child));
        }

        return item;
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    public void ExecuteSearch()
    {
        _matcher.LoadMatchExpression(SearchText);
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ResetVisibility(TreeItems, false);
        }
        else if (SearchText.Trim() == "!")
        {
            ResetVisibility(TreeItems, true);
        }
        else
        {
            SearchAndExpandNodes(TreeItems);
        }
    }

    private static void ResetVisibility(IEnumerable<TreeItemViewModel> items, bool keepHighlighting)
    {
        foreach (var item in items)
        {
            item.IsVisible = true;
            if (!keepHighlighting)
            {
                item.IsHighlighted = false;
            }

            ResetVisibility(item.Children, keepHighlighting);
        }
    }

    public void ExpandParents(string codeElementId)
    {
        var element = _codeGraph?.Nodes[codeElementId];
        while (element != null)
        {
            var vm = CodeElementIdToViewModel[element.Id];
            vm.IsExpanded = true;
            vm.IsVisible = true;
            element = element.Parent;
        }
    }

    private static void CollapseTree(IEnumerable<TreeItemViewModel> items)
    {
        foreach (var item in items)
        {
            CollapseTree(item.Children);
            item.IsExpanded = false;
        }
    }

    private bool SearchAndExpandNodes(IEnumerable<TreeItemViewModel> items)
    {
        var anyMatch = false;
        foreach (var item in items)
        {
            var matchesSearch = _matcher.IsMatch(item);
            var childrenMatch = SearchAndExpandNodes(item.Children);

            item.IsVisible = matchesSearch || childrenMatch;
            item.IsHighlighted = matchesSearch;

            if (childrenMatch)
            {
                item.IsExpanded = true;
                anyMatch = true;
            }

            anyMatch |= matchesSearch;
        }

        return anyMatch;
    }
}