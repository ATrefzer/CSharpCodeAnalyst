﻿using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Resources;
using Prism.Commands;

namespace CSharpCodeAnalyst.TreeArea;

public class TreeViewModel : INotifyPropertyChanged
{
    // For faster search
    private static readonly Dictionary<string, TreeItemViewModel> _codeElementIdToViewModel = new();
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

        SearchCommand = new DelegateCommand(ExecuteSearch);
        CollapseTreeCommand = new DelegateCommand(CollapseTree);
        ClearSearchCommand = new DelegateCommand(ClearSearch);
        DeleteFromModelCommand = new DelegateCommand<TreeItemViewModel>(DeleteFromModel);
        AddNodeToGraphCommand = new DelegateCommand<TreeItemViewModel>(AddNodeToGraph);
        _filteredTreeItems = [];
        _treeItems = [];
    }


    public ICommand CollapseTreeCommand { get; set; }

    public ICommand ClearSearchCommand { get; set; }


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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void DeleteFromModel(TreeItemViewModel obj)
    {
        var id = obj?.CodeElement?.Id;
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

    public void LoadCodeGraph(CodeGraph codeGraph)
    {
        _codeGraph = codeGraph;
        TreeItems.Clear();
        _codeElementIdToViewModel.Clear();

        SearchText = string.Empty;

        var rootNodes = codeGraph.Nodes.Values
            .Where(n => n.Parent == null)
            .OrderBy(n => n.Name);

        foreach (var rootNode in rootNodes)
        {
            TreeItems.Add(CreateTreeViewItem(rootNode));
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
        _codeElementIdToViewModel.Add(element.Id, item);


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

    private void ResetVisibility(IEnumerable<TreeItemViewModel> items, bool keepHighlighting)
    {
        foreach (var item in items)
        {
            item.IsVisible = true;
            if (keepHighlighting is false)
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
            var vm = _codeElementIdToViewModel[element.Id];
            vm.IsExpanded = true;
            vm.IsVisible = true;
            element = element.Parent;
        }
    }

    private void CollapseTree(IEnumerable<TreeItemViewModel> items)
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