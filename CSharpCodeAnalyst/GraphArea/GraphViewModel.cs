using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CodeParser.Extensions;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Exploration;
using CSharpCodeAnalyst.GraphArea.RenderOptions;
using CSharpCodeAnalyst.Help;
using CSharpCodeAnalyst.Resources;
using Prism.Commands;

namespace CSharpCodeAnalyst.GraphArea;

internal class GraphViewModel : INotifyPropertyChanged
{
    private readonly ICodeGraphExplorer _explorer;
    private readonly IPublisher _publisher;
    private readonly ApplicationSettings? _settings;
    private readonly LinkedList<GraphSession> _undoStack = new();
    private readonly int _undoStackSize = 10;
    private readonly IGraphViewer _viewer;

    private HighlightOption _selectedHighlightOption;
    private RenderOption _selectedRenderOption;
    private bool _showFlatGraph;

    internal GraphViewModel(IGraphViewer viewer, ICodeGraphExplorer explorer, IPublisher publisher,
        ApplicationSettings? settings)
    {
        _viewer = viewer;
        _explorer = explorer;
        _publisher = publisher;
        _settings = settings;

        // Initialize RenderOptions
        RenderOptions = new ObservableCollection<RenderOption>
        {
            new DefaultRenderOptions(),
            new LeftToRightRenderOptions(),
            new BottomToTopRenderOptions()
        };

        HighlightOptions = new ObservableCollection<HighlightOption>
        {
            HighlightOption.Default,
            new(HighlightMode.OutgoingEdgesChildrenAndSelf, Strings.HighlightOutgoingEdges),
            new(HighlightMode.ShortestNonSelfCircuit, Strings.HighlightSelfCircuit)
        };

        // Set defaults
        _selectedRenderOption = RenderOptions[0];
        _selectedHighlightOption = HighlightOptions[0];

        // Edge commands
        _viewer.AddContextMenuCommand(new RelationshipContextCommand(Strings.Delete, DeleteEdges));

        // Global commands
        _viewer.AddGlobalContextMenuCommand(
            new GlobalContextCommand(Strings.CompleteRelationships, CompleteDependencies));
        _viewer.AddGlobalContextMenuCommand(new GlobalContextCommand(Strings.CompleteToTypes, CompleteToTypes));
        _viewer.AddGlobalContextMenuCommand(new GlobalContextCommand(Strings.MarkedFocus, FocusOnMarkedElements,
            CanHandleIfMarkedElements));
        _viewer.AddGlobalContextMenuCommand(new GlobalContextCommand(Strings.MarkedDelete,
            DeleteMarkedWithChildren, CanHandleIfMarkedElements));
        _viewer.AddGlobalContextMenuCommand(new GlobalContextCommand(Strings.MarkedAddParent, AddParents,
            CanHandleIfMarkedElements));


        // Static commands
        _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.Expand, Expand, CanExpand));
        _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.Collapse, Collapse, CanCollapse));

        _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.Delete, DeleteWithoutChildren));
        _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.DeleteWithChildren, DeleteWithChildren));
        _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.FindInTree, FindInTreeRequest));
        _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.AddParent, AddParent));
        _viewer.AddContextMenuCommand(new SeparatorCommand());

        // Methods and properties
        HashSet<CodeElementType> elementTypes = [CodeElementType.Method, CodeElementType.Property];
        foreach (var elementType in elementTypes)
        {
            _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.FindOutgoingCalls, elementType,
                FindOutgoingCalls));
            _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.FindIncomingCalls, elementType,
                FindIncomingCalls));
            _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.FollowIncomingCalls, elementType,
                FollowIncomingCallsRecursive));
            _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.FindSpecializations, elementType,
                FindSpecializations));
            _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.FindAbstractions, elementType, FindAbstractions));
        }

        // Classes, structs and interfaces
        elementTypes = [CodeElementType.Class, CodeElementType.Interface, CodeElementType.Struct];
        foreach (var elementType in elementTypes)
        {
            _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.FindInheritanceTree, elementType,
                FindInheritanceTree));
            _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.FindSpecializations, elementType,
                FindSpecializations));
            _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.FindAbstractions, elementType, FindAbstractions));
        }

        // Events
        _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.FindSpecializations, CodeElementType.Event,
            FindSpecializations));
        _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.FindAbstractions, CodeElementType.Event,
            FindAbstractions));
        _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.FollowIncomingCalls, CodeElementType.Event,
            FollowIncomingCallsRecursive));


        // Everyone gets the in/out dependencies
        _viewer.AddContextMenuCommand(new SeparatorCommand());
        _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.AllIncomingRelationships, FindAllIncomingRelationships));
        _viewer.AddContextMenuCommand(new CodeElementContextCommand(Strings.AllOutgoingRelationships, FindAllOutgoingRelationships));

        UndoCommand = new DelegateCommand(Undo);
    }

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

            var codeGraph = _viewer.GetGraph();
            if (!ProceedWithLargeGraph(codeGraph.Nodes.Count))
            {
                // No collapsing in flat graph. We show everything
                return;
            }

            _showFlatGraph = value;
            _viewer.ShowFlatGraph(value);
            OnPropertyChanged(nameof(ShowFlatGraph));
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

    public ICommand UndoCommand { get; }


    public event PropertyChangedEventHandler? PropertyChanged;

    private bool CanHandleIfMarkedElements(List<CodeElement> markedElements)
    {
        return markedElements.Any();
    }

    private void FocusOnMarkedElements(List<CodeElement> markedElements)
    {
        // We want to include all children of the collapsed code elements
        // and keep also the presentation state. Just less information

        PushUndo();

        var session = _viewer.GetSession();
        var graph = _viewer.GetGraph();

        var idsToKeep = new HashSet<string>();

        // All children
        foreach (var element in markedElements)
        {
            var children = graph.Nodes[element.Id].GetChildrenIncludingSelf();
            idsToKeep.UnionWith(children);
        }

        var newGraph = graph.SubGraphOf(idsToKeep);

        // Cleanup unused states
        var idsToRemove = graph.Nodes.Keys.Except(idsToKeep).ToHashSet();

        var presentationState = session.PresentationState.Clone();
        presentationState.RemoveStates(idsToRemove);

        _viewer.LoadSession(newGraph, presentationState);
    }

    private void DeleteEdges(List<Relationship> dependencies)
    {
        PushUndo();
        _viewer.DeleteFromGraph(dependencies);
    }

    private void DeleteMarkedWithChildren(List<CodeElement> markedElements)
    {
        PushUndo();

        var graph = _viewer.GetGraph();
        var idsToRemove = new HashSet<string>();

        // Include children      
        foreach (var element in markedElements)
        {
            var children = graph.Nodes[element.Id].GetChildrenIncludingSelf();
            idsToRemove.UnionWith(children);
        }

        _viewer.DeleteFromGraph(idsToRemove);
    }

    private void CompleteToTypes(List<CodeElement> obj)
    {
        var viewerGraph = _viewer.GetGraph();
        var ids = viewerGraph.Nodes.Keys.ToHashSet();
        var result = _explorer.CompleteToContainingTypes(ids);
        AddToGraph(result.Elements, []);
    }

    private void CompleteDependencies(List<CodeElement> _)
    {
        // Not interested in the marked elements!
        var viewerGraph = _viewer.GetGraph();
        var ids = viewerGraph.Nodes.Keys.ToHashSet();
        var dependencies = _explorer.FindAllDependencies(ids);

        AddToGraph([], dependencies);
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

    private void DeleteWithoutChildren(CodeElement element)
    {
        PushUndo();
        _viewer.DeleteFromGraph([element.Id]);
    }

    private void DeleteWithChildren(CodeElement element)
    {
        PushUndo();
        var graph = _viewer.GetGraph();
        var idsToRemove = graph.Nodes[element.Id].GetChildrenIncludingSelf();
        _viewer.DeleteFromGraph(idsToRemove);
    }

    private void PushUndo()
    {
        if (_undoStack.Count >= _undoStackSize)
        {
            // Make space
            _undoStack.RemoveLast();
        }

        var session = _viewer.GetSession();
        _undoStack.AddFirst(session);
    }

    private void Undo()
    {
        if (_undoStack.Any() is false)
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
        _viewer.LoadSession(elements, state.Dependencies, state.PresentationState);
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

    private void AddToGraph(IEnumerable<CodeElement> originalCodeElements, IEnumerable<Relationship> dependencies)
    {
        PushUndo();
        _viewer.AddToGraph(originalCodeElements, dependencies);
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
        AddToGraph(new List<CodeElement> { request.Node }, []);
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

    internal void FindIncomingCalls(CodeElement method)
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


    internal void FollowIncomingCallsRecursive(CodeElement element)
    {
        if (!IsCallable(element))
        {
            return;
        }

        var result =
            _explorer.FollowIncomingCallsRecursive(element.Id);
        AddToGraph(result.Elements, result.Relationships);
    }

    internal void FindInheritanceTree(CodeElement? type)
    {
        if (type is null)
        {
            return;
        }

        var relationships =
            _explorer.FindFullInheritanceTree(type.Id);
        AddToGraph(relationships.Elements, relationships.Relationships);
    }

    internal void FindOutgoingCalls(CodeElement? method)
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

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    ///     Note that dependency type "Contains" is treated special
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

    private bool ProceedWithLargeGraph(int numberOfElements)
    {
        if (_settings is null)
        {
            return true;
        }

        // Meanwhile we collapse the graph.
        if (numberOfElements > _settings.WarningCodeElementLimit)
        {
            var msg = string.Format(Strings.TooMuchElementsMessage, numberOfElements);
            var title = Strings.TooMuchElementsTitle;
            var result = MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return MessageBoxResult.Yes == result;
        }

        return true;
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
        _viewer.LoadSession(elements, session.Dependencies, session.PresentationState);
    }
}