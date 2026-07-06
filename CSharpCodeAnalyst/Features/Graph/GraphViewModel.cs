using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CSharpCodeAnalyst.AnalyzerSdk.Contracts;
using CSharpCodeAnalyst.AnalyzerSdk.Messages;
using CSharpCodeAnalyst.AnalyzerSdk.Wpf;
using CSharpCodeAnalyst.CodeGraph.Contracts;
using CSharpCodeAnalyst.CodeGraph.Exploration;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Features.Graph.Filtering;
using CSharpCodeAnalyst.Features.Graph.RenderOptions;
using CSharpCodeAnalyst.Features.Refactoring;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.Services;
using CSharpCodeAnalyst.Shared.UI;
using CSharpCodeAnalyst.Shared.Wpf;

namespace CSharpCodeAnalyst.Features.Graph;

/// <summary>
///     Defines and handles the context menu commands for the graph viewer.
/// </summary>
internal sealed class GraphViewModel : INotifyPropertyChanged
{
    private const int UndoStackSize = 10;
    private readonly ICodeGraphExplorer _explorer;
    private readonly IPublisher _publisher;
    private readonly RefactoringService _refactoringService;
    private readonly AppSettings _settings;
    private readonly GraphViewState _state;
    private readonly LinkedList<GraphSession> _undoStack;

    private HighlightOption _selectedHighlightOption;
    private LayoutOption _selectedLayoutOption;

    internal GraphViewModel(GraphViewState state, ICodeGraphExplorer explorer, IPublisher publisher,
        AppSettings settings, RefactoringService refactoringService)
    {
        _undoStack = [];
        _state = state;
        _explorer = explorer;
        _publisher = publisher;
        _settings = settings;
        _refactoringService = refactoringService;
        DropHandler = new GraphDropHandler(publisher);

        HighlightOptions =
        [
            HighlightOption.Default,
            new HighlightOption(HighlightMode.OutgoingEdgesChildrenAndSelf, Strings.HighlightOutgoingEdges),
            new HighlightOption(HighlightMode.ShortestNonSelfCircuit, Strings.HighlightSelfCircuit)
        ];

        LayoutOptions =
        [
            LayoutOption.Default,
            new LayoutOption("dagre-tb", Strings.Layout_DagreTopBottom_Label),
            new LayoutOption("dagre-lr", Strings.Layout_DagreLeftRight_Label),
            new LayoutOption("elk-down", Strings.Layout_ElkDown_Label),
            new LayoutOption("elk-right", Strings.Layout_ElkRight_Label)
        ];

        // Set defaults
        _selectedHighlightOption = HighlightOptions[0];
        _selectedLayoutOption = LayoutOptions[0];

        var flag = IconLoader.LoadIcon("Resources/flag.png");
        var removeWithoutChildren = IconLoader.LoadIcon("Resources/remove_without_children_16.png");
        // Edge commands
        _state.AddCommand(new RelationshipContextCommand(string.Empty, Strings.ToggleFlag, ToggleEdgeFlag, icon: flag));
        _state.AddCommand(new RelationshipContextCommand(string.Empty, Strings.RemoveWithoutChildren, RemoveEdges, icon: removeWithoutChildren));
        _state.AddCommand(new RelationshipContextCommand(Strings.Refactor, Strings.Refactor_DeleteEdgeFromModel, DeleteEdgeFromModel));
        // Last: jump to code (always shown, grayed out unless the edge is a single relationship with a single source location).
        _state.AddCommand(new RelationshipContextCommand(string.Empty, Strings.JumpToCode, JumpToCodeEdge, canEnable: CanJumpToCodeEdge));


        // Static commands
        _state.AddCommand(new CodeElementContextCommand(Strings.Expand, Expand, CanExpand)
        {
            IsDoubleClickable = true,
            IsVisible = false
        });
        _state.AddCommand(new CodeElementContextCommand(Strings.Collapse, Collapse, CanCollapse)
        {
            IsDoubleClickable = true,
            IsVisible = false
        });



        var removeWithChildren = IconLoader.LoadIcon("Resources/remove_with_children_16.png");
        var findInTree = IconLoader.LoadIcon("Resources/find_in_tree_16.png");
        var addParent = IconLoader.LoadIcon("Resources/add_parent_16.png");
        _state.AddCommand(new CodeElementContextCommand(Strings.ToggleFlag, ToggleNodeFlag, icon: flag));
        _state.AddCommand(new CodeElementContextCommand(Strings.RemoveWithoutChildren, RemoveWithoutChildren, icon: removeWithoutChildren));
        _state.AddCommand(new CodeElementContextCommand(Strings.RemoveWithChildren, RemoveWithChildren, icon: removeWithChildren));
        _state.AddCommand(new CodeElementContextCommand(Strings.AddParent, OnAddParent, icon: addParent));
        _state.AddCommand(new SeparatorCommand());


        // Methods and properties
        var findSpecializations = IconLoader.LoadIcon("Resources/find_specializations_16.png");
        var findAbstractions = IconLoader.LoadIcon("Resources/find_abstractions_16.png");
        var incomingCalls = IconLoader.LoadIcon("Resources/incoming_calls_16.png");
        var followIncomingCalls = IconLoader.LoadIcon("Resources/follow_incoming_calls_16.png");
        var outgoingCalls = IconLoader.LoadIcon("Resources/outgoing_calls_16.png");
        // Property accessors (get_/set_) are method-like: they carry the same calls and
        // abstraction edges, so they get the same context menu entries as methods/properties.
        HashSet<CodeElementType> elementTypes = [CodeElementType.Method, CodeElementType.Property, CodeElementType.PropertyAccessor];
        foreach (var elementType in elementTypes)
        {
            _state.AddCommand(new CodeElementContextCommand(Strings.FindOutgoingCalls, elementType,
                FindOutgoingCalls, outgoingCalls));
            _state.AddCommand(new CodeElementContextCommand(Strings.FindIncomingCalls, elementType,
                FindIncomingCalls, incomingCalls));
            _state.AddCommand(new CodeElementContextCommand(Strings.FollowIncomingCalls, elementType,
                FollowIncomingCallsRecursive, followIncomingCalls));
            _state.AddCommand(new CodeElementContextCommand(Strings.FindSpecializations, elementType,
                FindSpecializations, findSpecializations));
            _state.AddCommand(new CodeElementContextCommand(Strings.FindAbstractions, elementType,
                FindAbstractions, findAbstractions));
        }

        // Classes, structs and interfaces
        var findInheritanceTree = IconLoader.LoadIcon("Resources/find_inheritance_tree_16.png");
        elementTypes = [CodeElementType.Class, CodeElementType.Interface, CodeElementType.Struct];
        foreach (var elementType in elementTypes)
        {
            _state.AddCommand(new CodeElementContextCommand(Strings.FindInheritanceTree, elementType,
                FindInheritanceTree, findInheritanceTree));
            _state.AddCommand(new CodeElementContextCommand(Strings.FindSpecializations, elementType,
                FindSpecializations, findSpecializations));
            _state.AddCommand(new CodeElementContextCommand(Strings.FindAbstractions, elementType,
                FindAbstractions, findAbstractions));
        }

        // Events
        _state.AddCommand(new CodeElementContextCommand(Strings.FindSpecializations, CodeElementType.Event,
            FindSpecializations, findSpecializations));
        _state.AddCommand(new CodeElementContextCommand(Strings.FindAbstractions, CodeElementType.Event,
            FindAbstractions, findAbstractions));
        _state.AddCommand(new CodeElementContextCommand(Strings.FollowIncomingCalls, CodeElementType.Event,
            FollowIncomingCallsRecursive, followIncomingCalls));


        // Everyone gets the in/out relationships
        _state.AddCommand(new SeparatorCommand());
        var incomingRelationships = IconLoader.LoadIcon("Resources/incoming_relationships_16.png");
        var outgoingRelationships = IconLoader.LoadIcon("Resources/outgoing_relationships_16.png");
        var exploreNeighbors = IconLoader.LoadIcon("Resources/explore_neighbors_16.png");

        _state.AddCommand(new CodeElementContextCommand(Strings.ExploreNeighbors,
            ExploreNeighbors, icon: exploreNeighbors));
        _state.AddCommand(new CodeElementContextCommand(Strings.AllIncomingRelationships,
            FindAllIncomingRelationships, icon: incomingRelationships));
        _state.AddCommand(new CodeElementContextCommand(Strings.AllOutgoingRelationships,
            FindAllOutgoingRelationships, icon: outgoingRelationships));

        _state.AddCommand(new CodeElementContextCommand(Strings.AllIncomingRelationshipsDeep,
            FindAllIncomingRelationshipsDeep));
        _state.AddCommand(new CodeElementContextCommand(Strings.AllOutgoingRelationshipsDeep,
            FindAllOutgoingRelationshipsDeep));

        _state.AddCommand(new CodeElementContextCommand(Strings.FocusOnIncomingDeep, FocusOnIncomingDeep));
        _state.AddCommand(new CodeElementContextCommand(Strings.FocusOnOutgoingDeep, FocusOnOutgoingDeep));


        /*
            Partition belongs to the tree view because it refers to all code elements inside the class.
            Included the ones not in the canvas. Therefore, it more logical to show the partition context menu in the tree.

        _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.Partition, CodeElementType.Class,
            PartitionClass));
        */

        _state.AddCommand(new SeparatorCommand());
        _state.AddCommand(new CodeElementContextCommand(Strings.FindInTree, FindInTreeRequest, icon: findInTree));

        // Last entry, consistent across all menus: jump to code (always shown, grayed out
        // unless the element has exactly one source location).
        _state.AddCommand(new CodeElementContextCommand(Strings.JumpToCode, JumpToCode, canEnable: CanJumpToCode));

        var copyIcon = IconLoader.LoadIcon("Resources/copy_full_path_16.png");
       
        _state.AddCommand(new CodeElementContextCommand(Strings.CopyFullPathToClipboard,
            OnCopyToClipboard, icon: copyIcon));

     


        UndoCommand = new WpfCommand(Undo);
        OpenGraphHideDialogCommand = new WpfCommand(OpenGraphHideDialog);

        // Toolbar
        CompleteToContainingTypesCommand = new WpfCommand(OnCompleteToContainingTypes);
        CompleteRelationshipsCommand = new WpfCommand(OnCompleteRelationships);
        CompleteRelationshipsDeepCommand = new WpfCommand(OnCompleteRelationshipsDeep);
        ClearAllFlagsCommand = new WpfCommand(OnClearAllFlags);
        FocusOnSelectedCommand = new WpfCommand(OnFocusOnSelected);
        ExpandEverythingCommand = new WpfCommand(OnExpandEverything);
        CollapseEverythingCommand = new WpfCommand(OnCollapseEverything);
        RemoveSelectedCommand = new WpfCommand(OnRemoveSelectedWithChildren);
        AddParentsCommand = new WpfCommand(OnAddParents);

        // Selection changes come from the web view, not WPF input events, so WpfCommand's
        // CommandManager.RequerySuggested would not otherwise re-evaluate CanExecute predicates that
        // depend on the selection. No toolbar command uses one yet, but this keeps it ready for when one does.
        _state.SelectionChanged += () => WpfCommand.RaiseCanExecuteChanged();

        // Global commands, moved to toolbar
        // _state.AddGlobalCommand(new GlobalCommand(Strings.CompleteRelationships, CompleteRelationships));
        // _state.AddGlobalCommand(new GlobalCommand(Strings.CompleteToTypes, CompleteToTypes));
        // _state.AddGlobalCommand(new GlobalCommand(Strings.SelectedFocus, Focus, CanHandleIfSelectedElements));
        // _state.AddGlobalCommand(new GlobalCommand(Strings.ClearAllFlags, ClearAllFlags));
        // _state.AddGlobalCommand(new GlobalCommand(Strings.ExpandEverything, ExpandEverything));
        //_state.AddGlobalCommand(new GlobalCommand(Strings.SelectedRemoveWithChildren, OnRemoveSelectedWithChildren, CanHandleIfSelectedElements, null, Key.Delete));
    }

    public ICommand CompleteToContainingTypesCommand { get; set; }
    public ICommand CompleteRelationshipsCommand { get; set; }
    public ICommand CompleteRelationshipsDeepCommand { get; set; }
    public ICommand UndoCommand { get; }
    public ICommand OpenGraphHideDialogCommand { get; }
    public ICommand ClearAllFlagsCommand { get; }
    public ICommand FocusOnSelectedCommand { get; }
    public ICommand ExpandEverythingCommand { get; }
    public ICommand RemoveSelectedCommand { get; }
    public ICommand CollapseEverythingCommand { get; }

    public ObservableCollection<HighlightOption> HighlightOptions { get; }

    public ObservableCollection<LayoutOption> LayoutOptions { get; }



    public bool ShowFlatGraph
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            _state.SetShowFlat(value);
            OnPropertyChanged(nameof(ShowFlatGraph));
        }
    }


    public bool ShowDataFlow
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            _state.SetShowInformationFlow(value);
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
            _state.SetHighlightMode(value.Mode);
            OnPropertyChanged(nameof(SelectedHighlightOption));
        }
    }

    public LayoutOption SelectedLayoutOption
    {
        get => _selectedLayoutOption;
        set
        {
            if (_selectedLayoutOption == value)
            {
                return;
            }

            _selectedLayoutOption = value;
            _state.SetLayout(value.Name);
            OnPropertyChanged(nameof(SelectedLayoutOption));
        }
    }

    public ICommand AddParentsCommand { get; }

    public GraphDropHandler DropHandler { get; }

    public event PropertyChangedEventHandler? PropertyChanged;



    private void DeleteEdgeFromModel(string sourceId, string targetId, List<Relationship> relationships)
    {
        _refactoringService.DeleteRelationships(relationships);
    }

    private void OnExpandEverything()
    {
        PushUndo();

        var session = _state.GetSession();
        var graph = _state.CodeGraph;
        var state = session.PresentationState;

        // Create a new presentation state with no collapsed nodes
        state.NodeIdToCollapsed.Clear();

        _state.LoadSession(graph, state);
    }

    private void OnCollapseEverything()
    {
        PushUndo();

        var session = _state.GetSession();
        var graph = _state.CodeGraph;
        var state = session.PresentationState;

        // Create a new presentation state with all collapsed nodes

        foreach (var nodeId in graph.Nodes.Keys)
        {
            if (graph.Nodes[nodeId].Children.Any())
            {
                state.SetCollapsedState(nodeId, true);
            }
        }

        _state.LoadSession(graph, state);
    }

    private void OnFocusOnSelected()
    {
        var selectedElementIds = _state.SelectedIds;
        if (!selectedElementIds.Any())
        {
            return;
        }

        // We want to include all children of the collapsed code elements
        // and keep also the presentation state. Just less information

        PushUndo();

        var session = _state.GetSession();
        var graph = _state.CodeGraph;

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

        _state.LoadSession(newGraph, presentationState);
    }

    private void OnClearAllFlags()
    {
        _state.ClearAllFlags();
    }

    private void OnCompleteRelationships()
    {
        var ids = GetSelectionOrAllPresentIds();
        var relationships = _explorer.FindAllRelationships(ids);

        AddToGraph([], relationships);
    }

    /// <summary>
    ///     Deep variant of <see cref="OnCompleteRelationships" />: also expands into the descendants of
    ///     the given roots, so relationships between not-yet-shown children (e.g. two methods of two
    ///     selected classes that call each other) are found and both the relationships and the newly
    ///     discovered elements are added. Newly discovered elements are added collapsed, so a deep
    ///     completion on a large selection does not blow up the canvas.
    /// </summary>
    private void OnCompleteRelationshipsDeep()
    {
        var ids = GetSelectionOrAllPresentIds();
        var result = _explorer.FindAllRelationshipsDeep(ids);
        AddToGraph(result.Elements, result.Relationships, addCollapsed: true);
    }

    /// <summary>
    ///     The selected elements, or (if nothing is selected) every element currently present on the
    ///     canvas - so these commands act on "everything shown" by default and narrow to the selection
    ///     once the user picks specific elements.
    /// </summary>
    private HashSet<string> GetSelectionOrAllPresentIds()
    {
        return _state.SelectedIds.Count > 0
            ? _state.SelectedIds.ToHashSet()
            : _state.CodeGraph.Nodes.Keys.ToHashSet();
    }

    private void OnCompleteToContainingTypes()
    {
        var viewerGraph = _state.CodeGraph;
        var ids = viewerGraph.Nodes.Keys.ToHashSet();
        var result = _explorer.FindMissingTypesForLonelyTypeMembers(ids);
        AddToGraph(result.Elements, []);
    }

    private void ToggleNodeFlag(CodeElement codeElement)
    {
        _state.ToggleFlag(codeElement.Id);
    }

    private void ToggleEdgeFlag(string sourceId, string targetId, List<Relationship> relationships)
    {
        if (relationships.Count == 0)
        {
            return;
        }

        _state.ToggleFlag(sourceId, targetId);
    }

    // Jump to code: only offered when there is exactly one source location (so it is hidden
    // on namespaces, multi-location elements and bundled edges).
    private static bool CanJumpToCode(CodeElement element)
    {
        return SourceLocationNavigator.CanJump(element);
    }

    private static void JumpToCode(CodeElement element)
    {
        SourceLocationNavigator.JumpTo(element);
    }

    private static bool CanJumpToCodeEdge(List<Relationship> relationships)
    {
        return SourceLocationNavigator.CanJump(relationships);
    }

    private static void JumpToCodeEdge(string sourceId, string targetId, List<Relationship> relationships)
    {
        SourceLocationNavigator.JumpTo(relationships);
    }

    private static void OnCopyToClipboard(CodeElement element)
    {
        var text = element?.FullName;
        if (text != null)
        {
            Clipboard.SetText(text);
        }
    }

    private void RemoveEdges(string sourceId, string targetId, List<Relationship> relationships)
    {
        PushUndo();
        _state.RemoveRelationships(relationships);
    }

    private void OnRemoveSelectedWithChildren()
    {
        var selectedElementIds = _state.SelectedIds;
        if (!selectedElementIds.Any())
        {
            return;
        }

        PushUndo();


        var graph = _state.CodeGraph;
        var idsToRemove = new HashSet<string>();

        // Include children      
        foreach (var elementId in selectedElementIds)
        {
            var children = graph.Nodes[elementId].GetChildrenIncludingSelf();
            idsToRemove.UnionWith(children);
        }

        _state.RemoveElements(idsToRemove);
    }

    private bool CanCollapse(CodeElement codeElement)
    {
        return !_state.IsCollapsed(codeElement.Id) &&
               codeElement.Children.Any();
    }

    private void Collapse(CodeElement codeElement)
    {
        PushUndo();
        _state.Collapse(codeElement.Id);
    }

    private bool CanExpand(CodeElement codeElement)
    {
        return _state.IsCollapsed(codeElement.Id) &&
               codeElement.Children.Any();
    }

    private void Expand(CodeElement codeElement)
    {
        PushUndo();
        _state.Expand(codeElement.Id);
    }

    private void OnAddParent(CodeElement codeElement)
    {
        AddParents([codeElement.Id]);
    }

    private void OnAddParents()
    {
        var elementIds = _state.SelectedIds.ToList();

        if (!elementIds.Any())
        {
            elementIds = _state.CodeGraph.GetRoots().Select(r => r.Id).ToList();
        }

        if (elementIds.Any())
        {
            AddParents(elementIds);
        }
    }

    private void AddParents(List<string> ids)
    {
        // We do not know the original graph.
        var result = _explorer.FindParents(ids);

        // Avoid flickering if parent is already part of the canvas.
        var newParents = result.Elements.Where(e => !_state.CodeGraph.Nodes.ContainsKey(e.Id));
        AddToGraph(newParents, []);
    }

    private void FindInTreeRequest(CodeElement codeElement)
    {
        _publisher.Publish(new LocateInTreeRequest(codeElement.Id));
    }

    private void RemoveWithoutChildren(CodeElement element)
    {
        PushUndo();
        _state.RemoveElements([element.Id]);
    }

    private void RemoveWithChildren(CodeElement element)
    {
        PushUndo();
        var graph = _state.CodeGraph;
        var idsToRemove = graph.Nodes[element.Id].GetChildrenIncludingSelf();
        _state.RemoveElements(idsToRemove);
    }

    private void PushUndo()
    {
        if (_undoStack.Count >= UndoStackSize)
        {
            // Make space
            _undoStack.RemoveLast();
        }

        var session = _state.GetSession();
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
        _state.LoadSession(elements, state.Relationships, state.PresentationState);
    }

    private void FindAllOutgoingRelationships(CodeElement element)
    {
        var result = _explorer.ExploreWithAccessors(element.Id, _explorer.FindOutgoingRelationships);
        AddToGraph(result.Elements, result.Relationships, focusId: element.Id);
    }

    /// <summary>
    ///     Adds the direct neighbors (both incoming and outgoing relationships) of the element in a
    ///     single step and collapsed. Only newly added elements are collapsed - neighbors already on
    ///     the canvas keep their state (see <see cref="GraphViewState.AddToGraph" />). When invoked on
    ///     a property, its accessors are pulled in via <see cref="ICodeGraphExplorer.ExploreWithAccessors" />.
    /// </summary>
    private void ExploreNeighbors(CodeElement element)
    {
        var incoming = _explorer.ExploreWithAccessors(element.Id, _explorer.FindIncomingRelationships);
        var outgoing = _explorer.ExploreWithAccessors(element.Id, _explorer.FindOutgoingRelationships);

        var elements = incoming.Elements.Union(outgoing.Elements);
        var relationships = incoming.Relationships.Union(outgoing.Relationships);

        AddToGraph(elements, relationships, true, element.Id);
    }

    private void FindAllIncomingRelationships(CodeElement element)
    {
        var result = _explorer.ExploreWithAccessors(element.Id, _explorer.FindIncomingRelationships);
        AddToGraph(result.Elements, result.Relationships, focusId: element.Id);
    }

    private void FindAllOutgoingRelationshipsDeep(CodeElement element)
    {
        var result = _explorer.ExploreWithAccessors(element.Id, _explorer.FindOutgoingRelationshipsDeep);
        AddToGraph(result.Elements, result.Relationships, true, element.Id);
    }

    private void FindAllIncomingRelationshipsDeep(CodeElement element)
    {
        var result = _explorer.ExploreWithAccessors(element.Id, _explorer.FindIncomingRelationshipsDeep);
        AddToGraph(result.Elements, result.Relationships, true, element.Id);
    }

    private void FocusOnOutgoingDeep(CodeElement element)
    {
        FocusAcrossBoundary(element, true);
    }

    private void FocusOnIncomingDeep(CodeElement element)
    {
        FocusAcrossBoundary(element, false);
    }

    /// <summary>
    ///     Reduces the canvas to the relationships that cross the boundary of the clicked container in
    ///     one direction. For <paramref name="outgoing" /> only edges that start somewhere inside the
    ///     container (any descendant, including itself) and end outside it survive; for incoming the
    ///     reverse. Only the endpoints of those edges remain - everything that does not participate in a
    ///     crossing edge is removed. Lets you break a large dependency cycle down into "what does this
    ///     part reach out to" / "who reaches into it".
    /// </summary>
    private void FocusAcrossBoundary(CodeElement element, bool outgoing)
    {
        var graph = _state.CodeGraph;
        if (!graph.Nodes.TryGetValue(element.Id, out var node))
        {
            return;
        }

        var inside = node.GetChildrenIncludingSelf();

        bool CrossesBoundary(Relationship relationship)
        {
            var sourceInside = inside.Contains(relationship.SourceId);
            var targetInside = inside.Contains(relationship.TargetId);
            return outgoing ? sourceInside && !targetInside : !sourceInside && targetInside;
        }

        var idsToKeep = new HashSet<string>();
        foreach (var relationship in graph.GetAllRelationships())
        {
            if (CrossesBoundary(relationship))
            {
                idsToKeep.Add(relationship.SourceId);
                idsToKeep.Add(relationship.TargetId);
                
                // Keep also the parent chain intact.
                var source = graph.Nodes[relationship.SourceId];
                var target = graph.Nodes[relationship.TargetId];

                var parentsInGraph = source.GetPathToRoot(false)
                    .Union(target.GetPathToRoot(false)).Select(e => e.Id);
                
                // The parents that are already in the graph.
                idsToKeep.UnionWith(parentsInGraph);
            }
        }
        
    

        if (idsToKeep.Count == 0)
        {
            // Nothing crosses the boundary in this direction - leave the canvas untouched.
            return;
        }

        PushUndo();

        var session = _state.GetSession();
        var newGraph = graph.Clone(CrossesBoundary, idsToKeep);

        var idsToRemove = graph.Nodes.Keys.Except(idsToKeep).ToHashSet();
        var presentationState = session.PresentationState.Clone();
        presentationState.RemoveStates(idsToRemove);

        _state.LoadSession(newGraph, presentationState);
    }

    private void FindSpecializations(CodeElement method)
    {
        var result = _explorer.ExploreWithAccessors(method.Id, _explorer.FindSpecializations);
        AddToGraph(result.Elements, result.Relationships, focusId: method.Id);
    }

    private void FindAbstractions(CodeElement method)
    {
        var result = _explorer.ExploreWithAccessors(method.Id, _explorer.FindAbstractions);
        AddToGraph(result.Elements, result.Relationships, focusId: method.Id);
    }

    private void AddToGraph(IEnumerable<CodeElement> originalCodeElements, IEnumerable<Relationship> relationships,
        bool addCollapsed = false, string? focusId = null)
    {
        var elementsToAdd = originalCodeElements.ToList();
        var relationshipsToAdd = relationships.ToList();

        if (elementsToAdd.Count == 0 && relationshipsToAdd.Count == 0)
        {
            // Don't trigger undo
            return;
        }

        PushUndo();

        // Let the render re-center on the anchor (e.g. the node an explore was invoked on),
        // so a full re-layout does not leave the user lost. Consumed by the next render.
        _state.SetFocusHint(focusId);

        // Apply "Automatically add containing type" setting
        if (_settings.AutomaticallyAddContainingType)
        {
            // Merge with existing ones so that we can fill container gaps directly
            var elementIds = elementsToAdd
                .Select(e => e.Id)
                .Union(_state.CodeGraph.Nodes.Keys).ToHashSet();

            var result = _explorer.FindMissingTypesForLonelyTypeMembers(elementIds);
            elementsToAdd.AddRange(result.Elements);
            relationshipsToAdd.AddRange(result.Relationships);
        }

        _state.AddToGraph(elementsToAdd, relationshipsToAdd, addCollapsed);
    }

    public void LoadCodeGraph(CodeGraph.Graph.CodeGraph codeGraph)
    {
        _explorer.LoadCodeGraph(codeGraph);
        Clear();
        _undoStack.Clear();
    }

    /// <summary>
    ///     Fills the canvas with the entire code graph, every container collapsed, to give an
    ///     overview right after importing a solution. Only the top-level roots stay visible and
    ///     edges roll up to them, so this is a "map" of the codebase rather than a wall of nodes.
    /// </summary>
    public void ShowCompleteGraphCollapsed(CodeGraph.Graph.CodeGraph codeGraph)
    {
        AddToGraph(codeGraph.Nodes.Values, codeGraph.GetAllRelationships(), true);
    }


    internal void HandleAddNodeToGraphRequest(AddNodeToGraphRequest request)
    {
        var nodes = request.Nodes.ToList();

        // Re-center on the added node when it is a single one (e.g. dragged from the tree);
        // for a batch there is no single anchor.
        var focusId = nodes.Count == 1 ? nodes[0].Id : null;
        AddToGraph(nodes, [], request.AddCollapsed, focusId);
    }

    /// <summary>
    ///     Expands every selected element by following relationships in the requested
    ///     direction (the keyboard shortcuts in the web graph). All selected elements are
    ///     processed together so the whole expansion is a single undo step; properties also
    ///     pull in their accessors via <see cref="ICodeGraphExplorer.ExploreWithAccessors" />.
    /// </summary>
    internal void HandleExploreSelectedRequest(ExploreSelectedRequest request)
    {
        var selectedIds = _state.SelectedIds.ToList();
        if (selectedIds.Count == 0)
        {
            return;
        }

        Func<string, SearchResult> explore = request.Direction switch
        {
            ExploreDirection.OutgoingRelationships => _explorer.FindOutgoingRelationships,
            ExploreDirection.IncomingRelationships => _explorer.FindIncomingRelationships,
            ExploreDirection.OutgoingRelationshipsDeep => _explorer.FindOutgoingRelationshipsDeep,
            ExploreDirection.IncomingRelationshipsDeep => _explorer.FindIncomingRelationshipsDeep,
            _ => _ => new SearchResult([], [])
        };

        var elements = new HashSet<CodeElement>();
        var relationships = new HashSet<Relationship>();
        foreach (var id in selectedIds)
        {
            var result = _explorer.ExploreWithAccessors(id, explore);
            elements.UnionWith(result.Elements);
            relationships.UnionWith(result.Relationships);
        }

        // The deep variants can pull in large subtrees; add them collapsed like the
        // per-element context-menu commands do.
        var addCollapsed = request.Direction is ExploreDirection.OutgoingRelationshipsDeep
            or ExploreDirection.IncomingRelationshipsDeep;

        // With a single selected node we can re-center on it; for a multi-selection there
        // is no single anchor, so we keep the default fit-to-graph.
        var focusId = selectedIds.Count == 1 ? selectedIds[0] : null;
        AddToGraph(elements, relationships, addCollapsed, focusId);
    }

    internal void Clear()
    {
        PushUndo();
        //_undoStack.Clear();
        _state.Clear();
    }

    private void FindIncomingCalls(CodeElement method)
    {
        if (!IsCallable(method))
        {
            return;
        }

        var callee = _explorer.ExploreWithAccessors(method.Id, _explorer.FindIncomingCalls);
        AddToGraph(callee.Elements, callee.Relationships, focusId: method.Id);
    }

    internal void FindIncomingCallsRecursive(CodeElement method)
    {
        if (!IsCallable(method))
        {
            return;
        }

        var callers = _explorer.ExploreWithAccessors(method.Id, _explorer.FindIncomingCallsRecursive);
        AddToGraph(callers.Elements, callers.Relationships, focusId: method.Id);
    }


    private void FollowIncomingCallsRecursive(CodeElement element)
    {
        if (!IsCallable(element))
        {
            return;
        }

        var result = _explorer.ExploreWithAccessors(element.Id, _explorer.FollowIncomingCallsHeuristically);
        AddToGraph(result.Elements, result.Relationships, focusId: element.Id);
    }

    private void FindInheritanceTree(CodeElement? type)
    {
        if (type is null)
        {
            return;
        }

        var relationships =
            _explorer.FindFullInheritanceTree(type.Id);
        AddToGraph(relationships.Elements, relationships.Relationships, focusId: type.Id);
    }

    private void FindOutgoingCalls(CodeElement? method)
    {
        if (method is null || !IsCallable(method))
        {
            return;
        }

        var callers = _explorer.ExploreWithAccessors(method.Id, _explorer.FindOutgoingCalls);
        AddToGraph(callers.Elements, callers.Relationships, focusId: method.Id);
    }

    private static bool IsCallable(CodeElement? method)
    {
        return method is { ElementType: CodeElementType.Method or CodeElementType.Property
            or CodeElementType.PropertyAccessor or CodeElementType.Event };
    }


    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    ///     Note that relationship type "Contains" is treated special
    /// </summary>
    public CodeGraph.Graph.CodeGraph ExportGraph()
    {
        return _state.CodeGraph;
    }

    public void ImportCycleGroup(CodeGraph.Graph.CodeGraph graph)
    {
        PushUndo();
        _state.Clear();

        // Everything is collapsed by default. This allows to import large graphs.
        var defaultState = graph.Nodes.Values.Where(c => c.Children.Any()).ToDictionary(c => c.Id, _ => true);
        var presentationState = new PresentationState(defaultState);

        var roots = graph.GetRoots();
        if (roots.Count == 1)
        {
            // Usability. If we have a single root, we expand it.
            presentationState.SetCollapsedState(roots[0].Id, false);
        }

        _state.LoadSession(graph, presentationState);
        WarnIfFiltersActive();
    }


    public GraphSession GetSession()
    {
        return _state.GetSession();
    }

    public CodeGraph.Graph.CodeGraph GetGraph()
    {
        return _state.CodeGraph;
    }

    public void LoadSession(GraphSession session, bool withUndo)
    {
        if (withUndo)
        {
            PushUndo();
        }

        var elements = _explorer.GetElements(session.CodeElementIds);
        _state.LoadSession(elements, session.Relationships, session.PresentationState);

        WarnIfFiltersActive();
    }

    private void WarnIfFiltersActive()
    {
        if (_settings.WarnIfFiltersActive)
        {
            var hideFilter = _state.HideFilter;
            if (hideFilter.IsActive())
            {
                // var hiddenCount = hideFilter.HiddenElementTypes.Count + hideFilter.HiddenRelationshipTypes.Count;
                ToastManager.ShowWarning(Strings.Message_FiltersAreActive, 4000);
            }
        }
    }

    internal void HandleRemoveSelectedRequest(RemoveSelectedElementsRequest request)
    {
        OnRemoveSelectedWithChildren();
    }

    private void OpenGraphHideDialog()
    {
        var currentFilter = _state.HideFilter;
        var viewModel = new GraphHideDialogViewModel(currentFilter);
        var dialog = new GraphHideDialog(viewModel)
        {
            Owner = Application.Current.MainWindow
        };

        var result = dialog.ShowDialog();
        if (result == true)
        {
            // Apply the filter from the dialog
            _state.SetHideFilter(viewModel.Filter);
        }
    }

    public void HandleCodeGraphRefactored(CodeGraphRefactored message)
    {
        // No undo because the old model does not exist anymore.

        var session = _state.GetSession();
        var canvasGraph = _state.CodeGraph;

        if (message is CodeElementsDeleted deleted)
        {
            // Any leftovers in the canvas get cleaned up.

            var newGraph = canvasGraph.Clone();
            newGraph.RemoveCodeElements(deleted.DeletedIds);

            // Cleanup unused states
            var presentationState = session.PresentationState.Clone();
            presentationState.RemoveStates(deleted.DeletedIds);

            _state.LoadSession(newGraph, presentationState);
        }
        else if (message is CodeElementsMoved moved)
        {
            // TODO May be sufficient to just check the source ids and not their children.
            // Add the same node ids with the same relationships. This fixes parent/child hierarchy.
            // We may have moved more nodes than in the graph. Or the graph is not affected at all by this movement.

            var relationships = canvasGraph.GetAllRelationships().ToList();
            var ids = canvasGraph.Nodes.Values.Select(n => n.Id).ToHashSet();

            // Is the canvas graph affected at all?
            var originalGraph = moved.Graph;
            var movedIds = new HashSet<string>();
            foreach (var element in moved.SourceIds.Select(movedId => originalGraph.Nodes[movedId]))
            {
                movedIds.UnionWith(element.GetChildrenIncludingSelf());
            }

            if (!movedIds.Intersect(ids).Any())
            {
                // None of the moved ids is in the graph
                return;
            }

            // Add the new parent to ensure the moved elements are visible with correct hierarchy
            // Since I cant move an assembly parent is never null    
            ids.Add(moved.NewParentId);

            // I use the old presentation state. Except the new parent node I should not see any different nodes.
            // However, the parent / child relationships have changed.
            var nodes = ids.Select(id => originalGraph.Nodes[id]).ToList();
            _state.LoadSession(nodes, relationships, session.PresentationState);
        }
        else if (message is RelationshipsDeleted relationshipsDeleted)
        {
            // Get rid of relationships in the canvas graph.
            _state.RemoveRelationships(relationshipsDeleted.Deleted);
        }

        // Added elements are for sure not in this graph yet.
    }
}
