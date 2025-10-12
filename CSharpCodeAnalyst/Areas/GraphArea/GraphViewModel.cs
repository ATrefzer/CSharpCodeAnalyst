using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CodeParser.Extensions;
using Contracts.Graph;
using CSharpCodeAnalyst.Areas.GraphArea.Filtering;
using CSharpCodeAnalyst.Areas.GraphArea.RenderOptions;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Exploration;
using CSharpCodeAnalyst.Help;
using CSharpCodeAnalyst.Messages;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.UI;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Areas.GraphArea;

/// <summary>
///     Defines and handles the context menu commands for the graph viewer.
/// </summary>
internal sealed class GraphViewModel : INotifyPropertyChanged
{
    private const int UndoStackSize = 10;
    private readonly ICodeGraphExplorer _explorer;
    private readonly IPublisher _publisher;
    private readonly ApplicationSettings _settings;
    private readonly LinkedList<GraphSession> _undoStack;
    private readonly IGraphViewer _viewer;

    private HighlightOption _selectedHighlightOption;
    private RenderOption _selectedRenderOption;
    private bool _showDataFlow;
    private bool _showFlatGraph;

    internal GraphViewModel(IGraphViewer viewer, ICodeGraphExplorer explorer, IPublisher publisher,
        ApplicationSettings settings)
    {
        _undoStack = [];
        _viewer = viewer;
        _explorer = explorer;
        _publisher = publisher;
        _settings = settings;

        // Initialize RenderOptions
        RenderOptions =
        [
            new DefaultRenderOptions(),
            new LeftToRightRenderOptions(),
            new BottomToTopRenderOptions()
        ];




        HighlightOptions =
        [
            HighlightOption.Default,
            new HighlightOption(HighlightMode.OutgoingEdgesChildrenAndSelf, Strings.HighlightOutgoingEdges),
            new HighlightOption(HighlightMode.ShortestNonSelfCircuit, Strings.HighlightSelfCircuit)
        ];

        // Set defaults
        _selectedRenderOption = RenderOptions[0];
        _selectedHighlightOption = HighlightOptions[0];

        var flag = IconLoader.LoadIcon("Resources/flag.png");

        // Edge commands
        _viewer.AddCommand(new RelationshipContextCommand(Strings.ToggleFlag, ToggleEdgeFlag, icon: flag));
        _viewer.AddCommand(new RelationshipContextCommand(Strings.RemoveWithoutChildren, RemoveEdges));


        // Static commands
        _viewer.AddCommand(new CodeElementContextCommand(Strings.Expand, Expand, CanExpand)
        {
            IsDoubleClickable = true,
            IsVisible = false
        });
        _viewer.AddCommand(new CodeElementContextCommand(Strings.Collapse, Collapse, CanCollapse)
        {
            IsDoubleClickable = true,
            IsVisible = false
        });

        _viewer.AddCommand(new CodeElementContextCommand(Strings.ToggleFlag, ToggleNodeFlag, icon: flag));
        _viewer.AddCommand(new CodeElementContextCommand(Strings.RemoveWithoutChildren, RemoveWithoutChildren));
        _viewer.AddCommand(new CodeElementContextCommand(Strings.RemoveWithChildren, RemoveWithChildren));
        _viewer.AddCommand(new CodeElementContextCommand(Strings.FindInTree, FindInTreeRequest));
        _viewer.AddCommand(new CodeElementContextCommand(Strings.AddParent, AddParent));
        _viewer.AddCommand(new SeparatorCommand());


        // Methods and properties
        HashSet<CodeElementType> elementTypes = [CodeElementType.Method, CodeElementType.Property];
        foreach (var elementType in elementTypes)
        {
            _viewer.AddCommand(new CodeElementContextCommand(Strings.FindOutgoingCalls, elementType,
                FindOutgoingCalls));
            _viewer.AddCommand(new CodeElementContextCommand(Strings.FindIncomingCalls, elementType,
                FindIncomingCalls));
            _viewer.AddCommand(new CodeElementContextCommand(Strings.FollowIncomingCalls, elementType,
                FollowIncomingCallsRecursive));
            _viewer.AddCommand(new CodeElementContextCommand(Strings.FindSpecializations, elementType,
                FindSpecializations));
            _viewer.AddCommand(new CodeElementContextCommand(Strings.FindAbstractions, elementType,
                FindAbstractions));
        }

        // Classes, structs and interfaces
        elementTypes = [CodeElementType.Class, CodeElementType.Interface, CodeElementType.Struct];
        foreach (var elementType in elementTypes)
        {
            _viewer.AddCommand(new CodeElementContextCommand(Strings.FindInheritanceTree, elementType,
                FindInheritanceTree));
            _viewer.AddCommand(new CodeElementContextCommand(Strings.FindSpecializations, elementType,
                FindSpecializations));
            _viewer.AddCommand(new CodeElementContextCommand(Strings.FindAbstractions, elementType,
                FindAbstractions));
        }

        // Events
        _viewer.AddCommand(new CodeElementContextCommand(Strings.FindSpecializations, CodeElementType.Event,
            FindSpecializations));
        _viewer.AddCommand(new CodeElementContextCommand(Strings.FindAbstractions, CodeElementType.Event,
            FindAbstractions));
        _viewer.AddCommand(new CodeElementContextCommand(Strings.FollowIncomingCalls, CodeElementType.Event,
            FollowIncomingCallsRecursive));


        // Everyone gets the in/out relationships
        _viewer.AddCommand(new SeparatorCommand());
        _viewer.AddCommand(new CodeElementContextCommand(Strings.AllIncomingRelationships,
            FindAllIncomingRelationships));
        _viewer.AddCommand(new CodeElementContextCommand(Strings.AllOutgoingRelationships,
            FindAllOutgoingRelationships));
        _viewer.AddCommand(new CodeElementContextCommand(Strings.AllIncomingRelationshipsDeep,
            FindAllIncomingRelationshipsDeep));
        _viewer.AddCommand(new CodeElementContextCommand(Strings.AllOutgoingRelationshipsDeep,
            FindAllOutgoingRelationshipsDeep));
        

        /*
            Partition belongs to the tree view because it refers to all code elements inside the class.
            Included the ones not in the canvas. Therefore, it more logical to show the partition context menu in the tree.

        _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.Partition, CodeElementType.Class,
            PartitionClass));
        */

        _viewer.AddCommand(new SeparatorCommand());
        _viewer.AddCommand(new CodeElementContextCommand(Strings.CopyFullQualifiedNameToClipboard,
            OnCopyToClipboard));


        UndoCommand = new WpfCommand(Undo);
        OpenGraphHideDialogCommand = new WpfCommand(OpenGraphHideDialog);

        // Toolbar
        CompleteToContainingTypesCommand = new WpfCommand(OnCompleteToContainingTypes);
        CompleteRelationshipsCommand = new WpfCommand(OnCompleteRelationships);
        ClearAllFlagsCommand = new WpfCommand(OnClearAllFlags);
        FocusOnSelectedCommand = new WpfCommand(OnFocusOnSelected);
        ExpandEverythingCommand = new WpfCommand(OnExpandEverything);
        RemoveSelectedCommand = new WpfCommand(OnRemoveSelectedWithChildren);
        
        // Global commands, moved to toolbar
        // _viewer.AddGlobalCommand(new GlobalCommand(Strings.CompleteRelationships, CompleteRelationships));
        // _viewer.AddGlobalCommand(new GlobalCommand(Strings.CompleteToTypes, CompleteToTypes));
        // _viewer.AddGlobalCommand(new GlobalCommand(Strings.SelectedFocus, Focus, CanHandleIfSelectedElements));
        // _viewer.AddGlobalCommand(new GlobalCommand(Strings.ClearAllFlags, ClearAllFlags));
        // _viewer.AddGlobalCommand(new GlobalCommand(Strings.ExpandEverything, ExpandEverything));
        //_viewer.AddGlobalCommand(new GlobalCommand(Strings.SelectedRemoveWithChildren, OnRemoveSelectedWithChildren, CanHandleIfSelectedElements, null, Key.Delete));
        
        
        // Not in toolbar yet. Did someone use it?
        // _viewer.AddGlobalCommand(new GlobalCommand(Strings.SelectedAddParent, AddParents, CanHandleIfSelectedElements));

    }

    public ICommand CompleteToContainingTypesCommand { get; set; }
    public ICommand CompleteRelationshipsCommand { get; set; }
    public ICommand UndoCommand { get; }
    public ICommand OpenGraphHideDialogCommand { get; }
    public ICommand ClearAllFlagsCommand { get; }
    public ICommand FocusOnSelectedCommand { get; }
    public ICommand ExpandEverythingCommand { get; }

    public ObservableCollection<HighlightOption> HighlightOptions { get; }


    public ObservableCollection<RenderOption> RenderOptions { get; }

    public RenderOption SelectedRenderOption
    {
        get => _selectedRenderOption;
        set
        {
            if (_selectedRenderOption != value)
            {
                _selectedRenderOption = value;
                OnPropertyChanged(nameof(SelectedRenderOption));
                UpdateGraphRenderOption();
            }
        }
    }



    public bool ShowFlatGraph
    {
        get => _showFlatGraph;
        set
        {
            if (value == _showFlatGraph)
            {
                return;
            }

            _showFlatGraph = value;
            _viewer.ShowFlatGraph(value);
            OnPropertyChanged(nameof(ShowFlatGraph));
        }
    }


    public bool ShowDataFlow
    {
        get => _showDataFlow;
        set
        {
            if (value == _showDataFlow)
            {
                return;
            }

            _showDataFlow = value;
            _viewer.ShowInformationFlow(value);
            OnPropertyChanged(nameof(ShowDataFlow));
        }
    }

    public HighlightOption SelectedHighlightOption
    {
        get => _selectedHighlightOption;
        set
        {
            if (_selectedHighlightOption == value)
            {
                return;
            }

            _selectedHighlightOption = value;
            _viewer.SetHighlightMode(value.Mode);
            OnPropertyChanged(nameof(SelectedHighlightOption));
        }
    }

    public ICommand RemoveSelectedCommand { get; }



    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnExpandEverything()
    {
        PushUndo();

        var session = _viewer.GetSession();
        var graph = _viewer.GetGraph();

        // Create a new presentation state with no collapsed nodes
        var newPresentationState = new PresentationState();

        // Copy the flagged states but not the collapsed states
        foreach (var nodeId in graph.Nodes.Keys)
        {
            if (session.PresentationState.IsFlagged(nodeId))
            {
                newPresentationState.SetFlaggedState(nodeId, true);
            }
        }

        _viewer.LoadSession(graph, newPresentationState);
    }

    private void OnFocusOnSelected()
    {
        var selectedElementIds = _viewer.GetSelectedElementIds();
        if (!selectedElementIds.Any())
        {
            return;
        }

        // We want to include all children of the collapsed code elements
        // and keep also the presentation state. Just less information

        PushUndo();

        var session = _viewer.GetSession();
        var graph = _viewer.GetGraph();

        var idsToKeep = new HashSet<string>();

        // All children of the current graph.
        foreach (var elementId in selectedElementIds)
        {
            var children = graph.Nodes[elementId].GetChildrenIncludingSelf();
            idsToKeep.UnionWith(children);
        }

        // Include only relationships to code elements in the subgraph
        var newGraph = graph.Clone(d => idsToKeep.Contains(d.TargetId), idsToKeep);

        // Cleanup unused states
        var idsToRemove = graph.Nodes.Keys.Except(idsToKeep).ToHashSet();

        var presentationState = session.PresentationState.Clone();
        presentationState.RemoveStates(idsToRemove);

        _viewer.LoadSession(newGraph, presentationState);
    }

    private void OnClearAllFlags()
    {
        _viewer.ClearAllFlags();
    }

    private void OnCompleteRelationships()
    {
        // Not interested in the selected elements!
        var viewerGraph = _viewer.GetGraph();
        var ids = viewerGraph.Nodes.Keys.ToHashSet();
        var relationships = _explorer.FindAllRelationships(ids);

        AddToGraph([], relationships);
    }

    private void OnCompleteToContainingTypes()
    {
        var viewerGraph = _viewer.GetGraph();
        var ids = viewerGraph.Nodes.Keys.ToHashSet();
        var result = _explorer.CompleteToContainingTypes(ids);
        AddToGraph(result.Elements, []);
    }

    private void ToggleNodeFlag(CodeElement codeElement)
    {
        _viewer.ToggleFlag(codeElement.Id);
    }

    private void ToggleEdgeFlag(string sourceId, string targetId, List<Relationship> relationships)
    {
        if (relationships.Count == 0)
        {
            return;
        }

        _viewer.ToggleFlag(sourceId, targetId, relationships);
    }

    private static void OnCopyToClipboard(CodeElement element)
    {
        var text = element?.FullName;
        if (text != null)
        {
            Clipboard.SetText(text);
        }
    }

    private static bool CanHandleIfSelectedElements(List<CodeElement> selectedElements)
    {
        return selectedElements.Any();
    }

    private void RemoveEdges(string sourceId, string targetId, List<Relationship> relationships)
    {
        PushUndo();
        _viewer.RemoveFromGraph(relationships);
    }

    private void OnRemoveSelectedWithChildren()
    {
        var selectedElementIds = _viewer.GetSelectedElementIds();
        if (!selectedElementIds.Any())
        {
            return;
        }
        
        PushUndo();
        

        var graph = _viewer.GetGraph();
        var idsToRemove = new HashSet<string>();

        // Include children      
        foreach (var elementId in selectedElementIds)
        {
            var children = graph.Nodes[elementId].GetChildrenIncludingSelf();
            idsToRemove.UnionWith(children);
        }

        _viewer.RemoveFromGraph(idsToRemove);
    }

    private void CompleteToTypes(List<CodeElement> _)
    {
        // Context menu
        OnCompleteToContainingTypes();
    }

    private void CompleteRelationships(List<CodeElement> _)
    {
        // Context menu
        OnCompleteRelationships();
    }

    private bool CanCollapse(CodeElement codeElement)
    {
        return !_viewer.IsCollapsed(codeElement.Id) &&
               codeElement.Children.Any();
    }

    private void Collapse(CodeElement codeElement)
    {
        PushUndo();
        _viewer.Collapse(codeElement.Id);
    }

    private bool CanExpand(CodeElement codeElement)
    {
        return _viewer.IsCollapsed(codeElement.Id) &&
               codeElement.Children.Any();
    }

    private void Expand(CodeElement codeElement)
    {
        PushUndo();
        _viewer.Expand(codeElement.Id);
    }

    private void AddParent(CodeElement codeElement)
    {
        AddParents([codeElement]);
    }

    private void AddParents(List<CodeElement> codeElements)
    {
        // We do not know the original graph.
        var ids = codeElements.Select(c => c.Id).ToList();
        var result = _explorer.FindParents(ids);
        AddToGraph(result.Elements, []);
    }

    private void FindInTreeRequest(CodeElement codeElement)
    {
        _publisher.Publish(new LocateInTreeRequest(codeElement.Id));
    }

    private void RemoveWithoutChildren(CodeElement element)
    {
        PushUndo();
        _viewer.RemoveFromGraph([element.Id]);
    }

    private void RemoveWithChildren(CodeElement element)
    {
        PushUndo();
        var graph = _viewer.GetGraph();
        var idsToRemove = graph.Nodes[element.Id].GetChildrenIncludingSelf();
        _viewer.RemoveFromGraph(idsToRemove);
    }

    private void PushUndo()
    {
        if (_undoStack.Count >= UndoStackSize)
        {
            // Make space
            _undoStack.RemoveLast();
        }

        var session = _viewer.GetSession();
        _undoStack.AddFirst(session);
    }

    private void Undo()
    {
        if (!_undoStack.Any())
        {
            MessageBox.Show(Strings.NothingToUndo_Message, Strings.NothingToUndo_Title, MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var state = _undoStack.First();
        _undoStack.RemoveFirst();

        // Restore code elements. We only save the ids in the persistence.
        var elements = _explorer.GetElements(state.CodeElementIds);

        // No undo stack operations while restoring the session.      
        _viewer.LoadSession(elements, state.Relationships, state.PresentationState);
    }

    private void UpdateGraphRenderOption()
    {
        _viewer.UpdateRenderOption(SelectedRenderOption);
    }

    private void FindAllOutgoingRelationships(CodeElement element)
    {
        var result = _explorer.FindOutgoingRelationships(element.Id);
        AddToGraph(result.Elements, result.Relationships);
    }

    private void FindAllIncomingRelationships(CodeElement element)
    {
        var result = _explorer.FindIncomingRelationships(element.Id);
        AddToGraph(result.Elements, result.Relationships);
    }
    
    private void FindAllOutgoingRelationshipsDeep(CodeElement element)
    {
        var result = _explorer.FindOutgoingRelationshipsDeep(element.Id);
        AddToGraph(result.Elements, result.Relationships, true);
    }

    private void FindAllIncomingRelationshipsDeep(CodeElement element)
    {
        var result = _explorer.FindIncomingRelationshipsDeep(element.Id);
        // Everything that is not yet in graph should be collapsed
        AddToGraph(result.Elements, result.Relationships, true);
    }

    private void FindSpecializations(CodeElement method)
    {
        var result = _explorer.FindSpecializations(method.Id);
        AddToGraph(result.Elements, result.Relationships);
    }

    private void FindAbstractions(CodeElement method)
    {
        var result = _explorer.FindAbstractions(method.Id);
        AddToGraph(result.Elements, result.Relationships);
    }

    private void AddToGraph(IEnumerable<CodeElement> originalCodeElements, IEnumerable<Relationship> relationships,
        bool addCollapsed = false)
    {
        PushUndo();

        var elementsToAdd = originalCodeElements.ToList();
        var relationshipsToAdd = relationships.ToList();

        // Apply "Automatically add containing type" setting
        if (_settings.AutomaticallyAddContainingType)
        {
            // Merge with existing ones so that we can fill container gaps directly
            var elementIds = elementsToAdd
                .Select(e => e.Id)
                .Union(_viewer.GetGraph().Nodes.Keys).ToHashSet();

            var result = _explorer.CompleteToContainingTypes(elementIds);
            elementsToAdd.AddRange(result.Elements);
            relationshipsToAdd.AddRange(result.Relationships);
        }

        _viewer.AddToGraph(elementsToAdd, relationshipsToAdd, addCollapsed);
    }

    public void LoadCodeGraph(CodeGraph codeGraph)
    {
        _explorer.LoadCodeGraph(codeGraph);
        Clear();
        _undoStack.Clear();

        // Only update after we change the code graph.
        _viewer.SetQuickInfoFactory(new QuickInfoFactory(codeGraph));
    }


    internal void HandleAddNodeToGraphRequest(AddNodeToGraphRequest request)
    {
        AddToGraph(request.Nodes.ToList(), [], request.AddCollapsed);
    }

    internal void Clear()
    {
        PushUndo();
        //_undoStack.Clear();
        _viewer.Clear();
    }

    internal void Layout()
    {
        _viewer.Layout();
    }

    private void FindIncomingCalls(CodeElement method)
    {
        if (!IsCallable(method))
        {
            return;
        }

        // Use the node from the original graph
        var callee = _explorer.FindIncomingCalls(method.Id);
        AddToGraph(callee.Methods, callee.Calls);
    }

    internal void FindIncomingCallsRecursive(CodeElement method)
    {
        if (!IsCallable(method))
        {
            return;
        }

        var callers =
            _explorer.FindIncomingCallsRecursive(method.Id);
        AddToGraph(callers.Methods, callers.Calls);
    }


    private void FollowIncomingCallsRecursive(CodeElement element)
    {
        if (!IsCallable(element))
        {
            return;
        }

        var result = _explorer.FollowIncomingCallsHeuristically(element.Id);

        AddToGraph(result.Elements, result.Relationships);
    }

    private void FindInheritanceTree(CodeElement? type)
    {
        if (type is null)
        {
            return;
        }

        var relationships =
            _explorer.FindFullInheritanceTree(type.Id);
        AddToGraph(relationships.Elements, relationships.Relationships);
    }

    private void FindOutgoingCalls(CodeElement? method)
    {
        if (method is null || !IsCallable(method))
        {
            return;
        }

        var callers = _explorer.FindOutgoingCalls(method.Id);
        AddToGraph(callers.Methods, callers.Calls);
    }

    private static bool IsCallable(CodeElement? method)
    {
        return method is { ElementType: CodeElementType.Method or CodeElementType.Property or CodeElementType.Event };
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    ///     Note that relationship type "Contains" is treated special
    /// </summary>
    public CodeGraph ExportGraph()
    {
        return _viewer.GetGraph();
    }

    public void SaveToSvg(FileStream stream)
    {
        _viewer.SaveToSvg(stream);
    }

    public void ShowGlobalContextMenu()
    {
        _viewer.ShowGlobalContextMenu();
    }

    public void ImportCycleGroup(CodeGraph graph)
    {
        PushUndo();
        _viewer.Clear();

        // Everything is collapsed by default. This allows to import large graphs.
        var defaultState = graph.Nodes.Values.Where(c => c.Children.Any()).ToDictionary(c => c.Id, _ => true);
        var presentationState = new PresentationState(defaultState);

        var roots = graph.GetRoots();
        if (roots.Count == 1)
        {
            // Usability. If we have a single root, we expand it.
            presentationState.SetCollapsedState(roots[0].Id, false);
        }

        _viewer.LoadSession(graph, presentationState);
        WarnIfFiltersActive();
    }


    public GraphSession GetSession()
    {
        return _viewer.GetSession();
    }

    public void LoadSession(GraphSession session, bool withUndo)
    {
        if (withUndo)
        {
            PushUndo();
        }

        var elements = _explorer.GetElements(session.CodeElementIds);
        _viewer.LoadSession(elements, session.Relationships, session.PresentationState);

        WarnIfFiltersActive();
    }

    private void WarnIfFiltersActive()
    {
        if (_settings.WarnIfFiltersActive)
        {
            var hideFilter = _viewer.GetHideFilter();
            if (hideFilter.IsActive())
            {
                // var hiddenCount = hideFilter.HiddenElementTypes.Count + hideFilter.HiddenRelationshipTypes.Count;
                ToastManager.ShowWarning(Strings.Message_FiltersAreActive, 4000);
            }
        }
    }

    public bool TryHandleKeyDown(KeyEventArgs keyEventArgs)
    {
        if (keyEventArgs.Key == Key.Delete)
        {
            OnRemoveSelectedWithChildren();
            return true;
        }

        return false;
    }

    private void OpenGraphHideDialog()
    {
        var currentFilter = _viewer.GetHideFilter();
        var viewModel = new GraphHideDialogViewModel(currentFilter);
        var dialog = new GraphHideDialog(viewModel)
        {
            Owner = Application.Current.MainWindow
        };

        var result = dialog.ShowDialog();
        if (result == true)
        {
            // Apply the filter from the dialog
            _viewer.SetHideFilter(viewModel.Filter);
        }
    }

    public void HandleCodeGraphRefactored(CodeGraphRefactored message)
    {
        // No  undo because the old model does not exist any more.

        var session = _viewer.GetSession();
        var canvasGraph = _viewer.GetGraph();

        if (message is CodeElementsDeleted deleted)
        {
            // Any leftovers in the canvas get cleaned up.

            var newGraph = canvasGraph.Clone();
            newGraph.RemoveCodeElements(deleted.DeletedIds);

            // Cleanup unused states
            var presentationState = session.PresentationState.Clone();
            presentationState.RemoveStates(deleted.DeletedIds);

            _viewer.LoadSession(newGraph, presentationState);
        }
        else if (message is CodeElementsMoved moved)
        {
            // Add the same node ids with the same relationships. This fixes parent/child hierarchy.
            // We may have moved more nodes than in the graph. Or the graph is not affected at all by this movement.

            var relationships = canvasGraph.GetAllRelationships().ToList();
            var ids = canvasGraph.Nodes.Values.Select(n => n.Id).ToHashSet();

            // Is the canvas graph affected at all?
            var originalGraph = moved.Graph;
            var movedIds = originalGraph.Nodes[moved.SourceId].GetChildrenIncludingSelf().ToHashSet();
            if (!movedIds.Intersect(ids).Any())
            {
                return;
            }

            // I don't know where the element was moved to. I add its parent.
            // Since I cant move an assembly parent is never null    
            ids.Add(moved.NewParentId);

            // I use the old presentation state. Except the new parent node I should not see any different nodes.
            // However, the parent / child relationships have changed.
            var nodes = ids.Select(id => originalGraph.Nodes[id]).ToList();
            _viewer.LoadSession(nodes, relationships, session.PresentationState);
        }
    }
}