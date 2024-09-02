using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private readonly LinkedList<GraphSessionState> _undoStack = new();
    private readonly int _undoStackSize = 10;
    private readonly IDependencyGraphViewer _viewer;

    private HighlightOption _selectedHighlightOption;
    private RenderOption _selectedRenderOption;
    private bool _showFlatGraph;
    private bool _undoStackLocked;


    internal GraphViewModel(IDependencyGraphViewer viewer, ICodeGraphExplorer explorer, IPublisher publisher,
        ApplicationSettings? settings)
    {
        viewer.BeforeChange += HandleBeforeChange;
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

        // Static commands
        _viewer.AddContextMenuCommand(new ContextCommand("Expand", Expand, CanExpand));
        _viewer.AddContextMenuCommand(new ContextCommand("Collapse", Collapse, CanCollapse));

        _viewer.AddContextMenuCommand(new ContextCommand("Delete", DeleteWithoutChildren));
        _viewer.AddContextMenuCommand(new ContextCommand("Delete (with children)", DeleteWithChildren));
        _viewer.AddContextMenuCommand(new ContextCommand("Find in tree", FindInTreeRequest));
        _viewer.AddContextMenuCommand(new ContextCommand("Add parent", AddParentRequest));
        _viewer.AddContextMenuCommand(new SeparatorCommand());

        var findOutgoingCalls = "Find outgoing Calls";
        var findIncomingCalls = "Find incoming Calls";
        var findIncomingCallsRecursive = "Find incoming Calls (recursive)";
        var findSpecializations = "Find specializations";
        var findAbstractions = "Find abstractions";


        // Methods
        _viewer.AddContextMenuCommand(new ContextCommand(findOutgoingCalls, CodeElementType.Method,
            FindOutgoingCalls));
        _viewer.AddContextMenuCommand(new ContextCommand(findIncomingCalls, CodeElementType.Method,
            FindIncomingCalls));
        _viewer.AddContextMenuCommand(new ContextCommand(findIncomingCallsRecursive, CodeElementType.Method,
            FindIncomingCallsRecursive));
        _viewer.AddContextMenuCommand(new ContextCommand(findSpecializations, CodeElementType.Method,
            FindSpecializations));
        _viewer.AddContextMenuCommand(new ContextCommand(findAbstractions, CodeElementType.Method,
            FindAbstractions));


        // Properties
        _viewer.AddContextMenuCommand(new ContextCommand(findOutgoingCalls, CodeElementType.Property,
            FindOutgoingCalls));
        _viewer.AddContextMenuCommand(new ContextCommand(findIncomingCalls, CodeElementType.Property,
            FindIncomingCalls));
        _viewer.AddContextMenuCommand(new ContextCommand(findIncomingCallsRecursive, CodeElementType.Property,
            FindIncomingCallsRecursive));
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

    private void CompleteDependencies(List<CodeElement> _)
    {
        // Not interested in the marked elements!
        var viewerGraph = _viewer.GetGraph();
        var ids = viewerGraph.Nodes.Keys.ToHashSet();
        var dependencies = _explorer.FindAllDependencies(ids);
        AddToGraph([], dependencies);
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

    private bool CanCollapse(CodeElement codeElement)
    {
        return !_viewer.IsCollapsed(codeElement.Id) &&
               codeElement.Children.Any();
    }

    private void Collapse(CodeElement codeElement)
    {
        _viewer.Collapse(codeElement.Id);
    }

    private bool CanExpand(CodeElement codeElement)
    {
        return _viewer.IsCollapsed(codeElement.Id) &&
               codeElement.Children.Any();
    }

    private void Expand(CodeElement codeElement)
    {
        _viewer.Expand(codeElement.Id);
    }

    private void AddParentRequest(CodeElement codeElement)
    {
        // We do not know the original graph.
        _publisher.Publish(new AddParentContainerRequest(codeElement.Id));
    }

    private void FindInTreeRequest(CodeElement codeElement)
    {
        _publisher.Publish(new LocateInTreeRequest(codeElement.Id));
    }

    private void DeleteWithoutChildren(CodeElement element)
    {
        _viewer.DeleteFromGraph([element.Id]);
    }

    private void DeleteWithChildren(CodeElement element)
    {
        var graph = _viewer.GetGraph();
        var idsToRemove = graph.Nodes[element.Id].GetChildrenIncludingSelf();
        _viewer.DeleteFromGraph(idsToRemove);
    }

    private void HandleBeforeChange(object? sender, EventArgs e)
    {
        PushUndo();
    }


    private void PushUndo()
    {
        if (_undoStackLocked)
        {
            return;
        }

        if (_undoStack.Count >= _undoStackSize)
        {
            // Make space
            _undoStack.RemoveLast();
        }

        var state = _viewer.GetSessionState();
        _undoStack.AddFirst(state);
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
        using (new UndoStackLock(this))
        {
            _viewer.RestoreSession(elements, state.Dependencies, state.PresentationState);
        }
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

    public void ImportCycleGroup(List<CodeElement> codeElements, List<Dependency> dependencies)
    {
        _viewer.ImportCycleGroup(codeElements, dependencies);
    }

    public GraphSessionState GetSessionState()
    {
        return _viewer.GetSessionState();
    }

    public void LoadSession(GraphSessionState session, bool withUndo)
    {
        var elements = _explorer.GetElements(session.CodeElementIds);

        if (withUndo)
        {
            _viewer.RestoreSession(elements, session.Dependencies, session.PresentationState);
        }
        else
        {
            using (new UndoStackLock(this))
            {
                _viewer.RestoreSession(elements, session.Dependencies, session.PresentationState);
            }
        }
    }

    private class UndoStackLock : IDisposable
    {
        private readonly GraphViewModel _viewModel;

        public UndoStackLock(GraphViewModel viewModel)
        {
            _viewModel = viewModel;
            _viewModel._undoStackLocked = true;
        }

        public void Dispose()
        {
            _viewModel._undoStackLocked = false;
        }
    }
}