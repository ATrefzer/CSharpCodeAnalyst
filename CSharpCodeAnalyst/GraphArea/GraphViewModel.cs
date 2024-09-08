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
using Prism.Commands;

namespace CSharpCodeAnalyst.GraphArea;

internal class GraphViewModel : INotifyPropertyChanged
{
    private readonly ICodeGraphExplorer _explorer;
    private readonly IPublisher _publisher;
    private readonly ApplicationSettings? _settings;
    private readonly LinkedList<GraphSession> _undoStack = new();
    private readonly int _undoStackSize = 10;
    private readonly IDependencyGraphViewer _viewer;

    private HighlightOption _selectedHighlightOption;
    private RenderOption _selectedRenderOption;
    private bool _showFlatGraph;

    internal GraphViewModel(IDependencyGraphViewer viewer, ICodeGraphExplorer explorer, IPublisher publisher,
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
            new(HighlightMode.OutgoingEdgesChildrenAndSelf, "Outgoing edges"),
            new(HighlightMode.ShortestNonSelfCircuit, "Shortest non self circuit")
        };

        // Set defaults
        _selectedRenderOption = RenderOptions[0];
        _selectedHighlightOption = HighlightOptions[0];

        // Global commands
        _viewer.AddGlobalContextMenuCommand(new GlobalContextCommand("Complete dependencies", CompleteDependencies));
        _viewer.AddGlobalContextMenuCommand(new GlobalContextCommand("Marked: Focus", FocusOnMarkedElements,
            CanHandleIfMarkedElements));
        _viewer.AddGlobalContextMenuCommand(new GlobalContextCommand("Marked: Delete (with children)",
            DeleteMarkedWithChildren, CanHandleIfMarkedElements));
        _viewer.AddGlobalContextMenuCommand(new GlobalContextCommand("Marked: Add Parent", AddParents,
            CanHandleIfMarkedElements));


        // Static commands
        _viewer.AddContextMenuCommand(new ContextCommand("Expand", Expand, CanExpand));
        _viewer.AddContextMenuCommand(new ContextCommand("Collapse", Collapse, CanCollapse));

        _viewer.AddContextMenuCommand(new ContextCommand("Delete", DeleteWithoutChildren));
        _viewer.AddContextMenuCommand(new ContextCommand("Delete (with children)", DeleteWithChildren));
        _viewer.AddContextMenuCommand(new ContextCommand("Find in tree", FindInTreeRequest));
        _viewer.AddContextMenuCommand(new ContextCommand("Add parent", AddParent));
        _viewer.AddContextMenuCommand(new SeparatorCommand());

        var findOutgoingCalls = "Find outgoing Calls";
        var findIncomingCalls = "Find incoming Calls";
        var followIncomingCalls = "Follow incoming Calls";
        var findSpecializations = "Find specializations";
        var findAbstractions = "Find abstractions";


        // Methods
        _viewer.AddContextMenuCommand(new ContextCommand(findOutgoingCalls, CodeElementType.Method,
            FindOutgoingCalls));
        _viewer.AddContextMenuCommand(new ContextCommand(findIncomingCalls, CodeElementType.Method,
            FindIncomingCalls));
        //_viewer.AddContextMenuCommand(new ContextCommand(findIncomingCallsRecursive, CodeElementType.Method,
        //    FindIncomingCallsRecursive));
        _viewer.AddContextMenuCommand(new ContextCommand(followIncomingCalls, CodeElementType.Method,
            FollowIncomingCallsRecursive));
        _viewer.AddContextMenuCommand(new ContextCommand(findSpecializations, CodeElementType.Method,
            FindSpecializations));
        _viewer.AddContextMenuCommand(new ContextCommand(findAbstractions, CodeElementType.Method,
            FindAbstractions));


        // Properties
        _viewer.AddContextMenuCommand(new ContextCommand(findOutgoingCalls, CodeElementType.Property,
            FindOutgoingCalls));
        _viewer.AddContextMenuCommand(new ContextCommand(findIncomingCalls, CodeElementType.Property,
            FindIncomingCalls));
        //_viewer.AddContextMenuCommand(new ContextCommand(findIncomingCallsRecursive, CodeElementType.Property,
        //    FindIncomingCallsRecursive));
        _viewer.AddContextMenuCommand(new ContextCommand(followIncomingCalls, CodeElementType.Property,
            FollowIncomingCallsRecursive));
        _viewer.AddContextMenuCommand(
            new ContextCommand(findSpecializations, CodeElementType.Property, FindSpecializations));
        _viewer.AddContextMenuCommand(new ContextCommand(findAbstractions, CodeElementType.Property,
            FindAbstractions));


        // Classes
        _viewer.AddContextMenuCommand(new ContextCommand("Find full inheritance tree", CodeElementType.Class,
            FindSpecializationAndAbstractions));
        _viewer.AddContextMenuCommand(new ContextCommand(findSpecializations, CodeElementType.Class,
            FindSpecializations));
        _viewer.AddContextMenuCommand(new ContextCommand(findAbstractions, CodeElementType.Class, FindAbstractions));

        // Interfaces
        _viewer.AddContextMenuCommand(new ContextCommand("Find full inheritance tree", CodeElementType.Interface,
            FindSpecializationAndAbstractions));
        _viewer.AddContextMenuCommand(new ContextCommand(findSpecializations, CodeElementType.Interface,
            FindSpecializations));
        _viewer.AddContextMenuCommand(new ContextCommand(findAbstractions, CodeElementType.Interface,
            FindAbstractions));


        // Everyone gets the in/out dependencies
        _viewer.AddContextMenuCommand(new SeparatorCommand());
        _viewer.AddContextMenuCommand(new ContextCommand("All incoming dependencies", FindAllIncomingDependencies));
        _viewer.AddContextMenuCommand(new ContextCommand("All outgoing dependencies", FindAllOutgoingDependencies));

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
            MessageBox.Show("Nothing to undo!", "", MessageBoxButton.OK, MessageBoxImage.Information);
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

    private void FindAllOutgoingDependencies(CodeElement element)
    {
        var result = _explorer.FindOutgoingDependencies(element.Id);
        AddToGraph(result.Elements, result.Dependencies);
    }

    private void FindAllIncomingDependencies(CodeElement element)
    {
        var result = _explorer.FindIncomingDependencies(element.Id);
        AddToGraph(result.Elements, result.Dependencies);
    }

    private void FindSpecializations(CodeElement method)
    {
        var result = _explorer.FindSpecializations(method.Id);
        AddToGraph(result.Elements, result.Dependencies);
    }

    private void FindAbstractions(CodeElement method)
    {
        var result = _explorer.FindAbstractions(method.Id);
        AddToGraph(result.Elements, result.Dependencies);
    }

    private void AddToGraph(IEnumerable<CodeElement> originalCodeElements, IEnumerable<Dependency> dependencies)
    {
        PushUndo();
        _viewer.AddToGraph(originalCodeElements, dependencies);
    }

    public void LoadCodeGraph(CodeGraph codeGraph)
    {
        _explorer.LoadCodeGraph(codeGraph);
        Clear();

        // Only update after we change the code graph.
        _viewer.SetQuickInfoFactory(new QuickInfoFactory(codeGraph));
    }


    internal void HandleAddNodeToGraphRequest(AddNodeToGraphRequest request)
    {
        AddToGraph(new List<CodeElement> { request.Node }, []);
    }

    internal void Clear()
    {
        _undoStack.Clear();
        _viewer.Clear();
    }

    internal void Layout()
    {
        _viewer.Layout();
    }

    internal void FindIncomingCalls(CodeElement method)
    {
        if (!IsMethodOrProperty(method))
        {
            return;
        }

        // Use the node from the original graph
        var callee = _explorer.FindIncomingCalls(method.Id);
        AddToGraph(callee.Methods, callee.Calls);
    }

    internal void FindIncomingCallsRecursive(CodeElement method)
    {
        if (!IsMethodOrProperty(method))
        {
            return;
        }

        var callers =
            _explorer.FindIncomingCallsRecursive(method.Id);
        AddToGraph(callers.Methods, callers.Calls);
    }


    internal void FollowIncomingCallsRecursive(CodeElement method)
    {
        if (!IsMethodOrProperty(method))
        {
            return;
        }

        var result =
            _explorer.FollowIncomingCallsRecursive(method.Id);
        AddToGraph(result.Elements, result.Dependencies);
    }

    internal void FindSpecializationAndAbstractions(CodeElement? type)
    {
        if (type is null)
        {
            return;
        }

        var relationships =
            _explorer.FindFullInheritanceTree(type.Id);
        AddToGraph(relationships.Elements, relationships.Dependencies);
    }

    internal void FindOutgoingCalls(CodeElement? method)
    {
        if (method is null || !IsMethodOrProperty(method))
        {
            return;
        }

        var callers = _explorer.FindOutgoingCalls(method.Id);
        AddToGraph(callers.Methods, callers.Calls);
    }

    private static bool IsMethodOrProperty(CodeElement? method)
    {
        return method is { ElementType: CodeElementType.Method or CodeElementType.Property };
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
            var result = MessageBox.Show(
                $"There are {numberOfElements} code elements in this cycle. It may take a long time to render this data. Do you want to proceed?",
                "Proceed?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
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

        PushUndo();
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