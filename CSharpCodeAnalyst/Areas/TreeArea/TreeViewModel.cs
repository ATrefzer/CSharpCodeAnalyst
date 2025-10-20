using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Messages;
using CSharpCodeAnalyst.Refactoring;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Areas.TreeArea;

public class TreeViewModel : INotifyPropertyChanged
{
    // For faster search
    private static readonly Dictionary<string, TreeItemViewModel> CodeElementIdToViewModel = new();
    private readonly MessageBus _messaging;
    private readonly RefactoringService _refactoringService;
    private CodeGraph? _codeGraph;
    private string? _lastSelectedCodeElement;
    private string _searchText;
    private ObservableCollection<TreeItemViewModel> _treeItems;

    public TreeViewModel(MessageBus messaging, RefactoringService refactoringService)
    {
        _messaging = messaging;
        _refactoringService = refactoringService;
        _searchText = string.Empty;

        SearchCommand = new WpfCommand(ExecuteSearch);
        CollapseTreeCommand = new WpfCommand(CollapseTree);
        ClearSearchCommand = new WpfCommand(ClearSearch);
        AddNodeToGraphCommand = new WpfCommand<TreeItemViewModel>(AddNodeToGraph);
        PartitionTreeCommand = new WpfCommand<TreeItemViewModel>(Partition, CanPartition);
        PartitionWithBaseTreeCommand = new WpfCommand<TreeItemViewModel>(PartitionWithBase, CanPartition);
        CopyToClipboardCommand = new WpfCommand<TreeItemViewModel>(OnCopyToClipboard);

        // Refactoring
        DeleteFromModelCommand = new WpfCommand<TreeItemViewModel>(RefactoringDeleteCodeElement);
        CreateCodeElementCommand = new WpfCommand<TreeItemViewModel>(RefactoringCreateCodeElement, RefactoringCanCreateCodeElement);

        SetMovementTargetCommand = new WpfCommand<TreeItemViewModel>(RefactoringSetMovementTarget, RefactoringCanSetMovementTarget);
        MoveCommand = new WpfCommand<TreeItemViewModel>(RefactoringMoveCodeElement, RefactoringCanMoveCodeElement);
        SelectedItemChangedCommand = new WpfCommand<TreeItemViewModel>(OnSelectedItemChanged);

        _treeItems = [];
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

    public ICommand SetMovementTargetCommand { get; private set; }
    public ICommand MoveCommand { get; private set; }

    public ICommand SelectedItemChangedCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnSelectedItemChanged(TreeItemViewModel item)
    {
        if (item?.CodeElement != null)
        {
            _lastSelectedCodeElement = item.CodeElement.Id;
        }
    }

    private bool RefactoringCanMoveCodeElement(TreeItemViewModel tvm)
    {
        return _refactoringService.CanMoveCodeElement(tvm?.CodeElement?.Id);
    }

    private void RefactoringMoveCodeElement(TreeItemViewModel? tvm)
    {
        _refactoringService.MoveCodeElement(tvm?.CodeElement?.Id);
    }

    private bool RefactoringCanSetMovementTarget(TreeItemViewModel tvm)
    {
        return _refactoringService.CanSetMovementTarget(tvm?.CodeElement?.Id);
    }

    private void RefactoringSetMovementTarget(TreeItemViewModel tvm)
    {
        _refactoringService.SetMovementTarget(tvm?.CodeElement?.Id);
    }



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


    public void HandleCodeGraphRefactored(CodeGraphRefactored message)
    {
        // Note: Graph is actually the same

        // Tree updates are slow, so do the update manually.
        if (message is CodeElementCreated created)
        {
            RefactoringCodeElementAdded(created);
        }
        else if (message is CodeElementsDeleted deleted)
        {
            RefactoringCodeElementDeleted(deleted.DeletedElementId, deleted.ParentId, deleted.DeletedIds);
        }
        else if (message is CodeElementsMoved moved)
        {
            // This may be slow but easy.
            LoadCodeGraph(moved.Graph);
            _messaging.Publish(new LocateInTreeRequest(moved.SourceId));
        }
        else if (message is RelationshipsDeleted relationshipsDeleted)
        {
            // We already have the latest code graph, so no action needed.
            Debug.Assert(!_codeGraph!.DeleteRelationships(relationshipsDeleted.Deleted));
        }
    }

    private void RefactoringCodeElementDeleted(string deletedElementId, string? parentId, HashSet<string> deletedIds)
    {
        // Refresh the tree to show the new element

        // Delete from tree.
        if (!CodeElementIdToViewModel.TryGetValue(deletedElementId, out _))
        {
            // Code element not found
            return;
        }

        if (parentId != null && CodeElementIdToViewModel.TryGetValue(parentId, out var parentViewModel))
        {
            var item = parentViewModel.Children.FirstOrDefault(x => x.CodeElement is not null && x.CodeElement.Id == deletedElementId);
            if (item != null)
            {
                parentViewModel.Children.Remove(item);
            }
        }
        else
        {
            // Delete root element
            var item = TreeItems.FirstOrDefault(x => x.CodeElement is not null && x.CodeElement.Id == deletedElementId);
            if (item != null)
            {
                TreeItems.Remove(item);
            }
        }

        // Cleanup search index
        foreach (var id in deletedIds)
        {
            CodeElementIdToViewModel.Remove(id);
        }
    }

    private void RefactoringCodeElementAdded(CodeElementCreated created)
    {
        // Update the tree to show the new element
        var newElement = created.NewElement;
        var newTreeItem = CreateTreeViewItem(newElement);
        var parent = newElement.Parent;

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

    /// <summary>
    ///     Creates a code element at the root level (e.g., Assembly).
    ///     Public method to be called from UI when right-clicking empty space.
    /// </summary>
    public void RefactoringCreateCodeElementAtRoot()
    {
        RefactoringCreateCodeElement(null);
    }

    private bool RefactoringCanCreateCodeElement(TreeItemViewModel? tvm)
    {
        // null tvm means root level (empty space in tree) - this is allowed
        // Otherwise check if the CodeElement can have children
        return _refactoringService.CanCreateCodeElement(tvm?.CodeElement?.Id);
    }


    private void RefactoringCreateCodeElement(TreeItemViewModel? item)
    {
        var parentId = item?.CodeElement?.Id; // null means root level
        _refactoringService.CreateCodeElement(parentId);
    }

    private void RefactoringDeleteCodeElement(TreeItemViewModel tvi)
    {
        var codeElement = tvi.CodeElement;
        var id = codeElement?.Id;

        _refactoringService.DeleteCodeElementAndAllChildren(id);
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
        var externalRoots = rootNodes.Where(n => n.IsExternal).OrderBy(n => n.Name).ToList();

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

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    public void ExecuteSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ResetVisibility(TreeItems, false);
        }
        else if (SearchText.Trim() == "!")
        {
            ResetVisibility(TreeItems, true);
            if (!string.IsNullOrEmpty(_lastSelectedCodeElement))
            {
                // Since I want to keep the highlighting, I likely want to keep the location.
                if ((bool)_codeGraph?.Nodes.ContainsKey(_lastSelectedCodeElement))
                {
                    _messaging.Publish(new LocateInTreeRequest(_lastSelectedCodeElement));
                }
            }
        }
        else
        {
            var expr = SearchExpressionFactory.CreateSearchExpression(SearchText, SearchExpressionFactory.TextSearchField.Name);
            SearchAndExpandNodes(TreeItems, expr);
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

    private bool SearchAndExpandNodes(IEnumerable<TreeItemViewModel> items, IExpression expr)
    {
        var anyMatch = false;
        foreach (var item in items)
        {
            var matchesSearch = expr.Evaluate(item.CodeElement);
            var childrenMatch = SearchAndExpandNodes(item.Children, expr);

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

    public string GetRefactoringNewMoveParent()
    {
        var target = _refactoringService.GetMovementTarget();
        return target?.Name != null ? target.Name : string.Empty;
    }
}