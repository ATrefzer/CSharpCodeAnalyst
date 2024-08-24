using CodeParser.Extensions;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.GraphArea.Highlighig;
using CSharpCodeAnalyst.GraphArea.RenderOptions;
using CSharpCodeAnalyst.Help;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using Node = Microsoft.Msagl.Drawing.Node;

namespace CSharpCodeAnalyst.GraphArea;

/// <summary>
///     Note:
///     Between nodes we can have multiple dependencies if the dependency type is different.
///     Dependencies of the same type (i.e a method Calls another multiple times) are handled
///     in the parser. In this case the dependency holds all source references.
/// </summary>
internal partial class DependencyGraphViewer : IDependencyGraphViewer, IDependencyGraphBinding, INotifyPropertyChanged
{
    private readonly List<IContextCommand> _contextCommands = [];
    private readonly MsaglBuilder _msaglBuilder;
    private readonly IPublisher _publisher;

    private readonly LinkedList<UndoState> _undoStack = new();

    private readonly int _undoStackSize = 10;

    /// <summary>
    ///     Held to read the help
    /// </summary>
    private IViewerObject? _clickedObject;
    private CodeGraph _clonedCodeGraph = new();
    private IQuickInfoFactory? _factory;
    private GraphViewer? _msaglViewer;
    private PresentationState _presentationState = new();
    private RenderOption _renderOption = new DefaultRenderOptions();
    private bool _showFlatGraph;

    /// <summary>
    ///     Note:
    ///     Between nodes we can have multiple dependencies if the dependency type is different.
    ///     Dependencies of the same type (i.e a method Calls another multiple times) are handled
    ///     in the parser. In this case the dependency holds all source references.
    /// </summary>
    public DependencyGraphViewer(IPublisher publisher)
    {
        _publisher = publisher;
        _msaglBuilder = new MsaglBuilder();
        SetHighlightMode(HighlightMode.EdgeHovered);
    }
    public void Bind(Panel graphPanel)
    {
        _msaglViewer = new GraphViewer();
        _msaglViewer.BindToPanel(graphPanel);

        _msaglViewer.ObjectUnderMouseCursorChanged += ObjectUnderMouseCursorChanged;
        _msaglViewer.MouseDown += Viewer_MouseDown!;
    }

    public void ShowFlatGraph(bool value)
    {
        _showFlatGraph = value;
        RefreshGraph();
    }

    private void AddToGraphInternal(IEnumerable<CodeElement> originalCodeElements, IEnumerable<Dependency> newDependencies)
    {
        if (_msaglViewer is null)
        {
            return;
        }

        IntegrateNewFromOriginal(originalCodeElements);

        // Add dependencies we explicitly requested.
        foreach (var newDependency in newDependencies)
        {
            var sourceElement = _clonedCodeGraph.Nodes[newDependency.SourceId];
            sourceElement.Dependencies.Add(newDependency);
        }

        RefreshGraph();
    }

    /// <summary>
    ///     Adding an existing element or dependency is prevented.
    ///     Note from the originalCodeElement we don't add parent or children.
    ///     We just use this information to integrate the node into the existing canvas.
    /// </summary>
    public void AddToGraph(IEnumerable<CodeElement> originalCodeElements, IEnumerable<Dependency> newDependencies)
    {
        if (_msaglViewer is null)
        {
            return;
        }

        PushUndo();
        AddToGraphInternal(originalCodeElements, newDependencies);
    }

    public void AddContextCommand(IContextCommand command)
    {
        _contextCommands.Add(command);
    }

    private void Clear(bool withUndoStack)
    {
        if (_msaglViewer is null)
        {
            return;
        }

        _clonedCodeGraph = new CodeGraph();

        if (withUndoStack)
        {
            ClearUndo();
        }

        // Nothing collapsed by default
        _presentationState = new PresentationState();
        RefreshGraph();
    }
    public void Clear()
    {
        Clear(true);
    }
    private void ClearUndo()
    {
        _undoStack.Clear();
    }

    public void Layout()
    {
        //_msaglViewer?.SetInitialTransform();
        RefreshGraph();
    }

    public CodeGraph GetStructure()
    {
        return _clonedCodeGraph;
    }

    public void UpdateRenderOption(RenderOption renderOption)
    {
        _renderOption = renderOption;
        RefreshGraph();
    }

    public void SaveToSvg(FileStream stream)
    {
        if (_msaglViewer is null)
        {
            return;
        }

        var writer = new SvgGraphWriter(stream, _msaglViewer.Graph);
        writer.Write();
    }

    public void SetHighlightMode(HighlightMode valueMode)
    {
        _activeHighlighting?.Clear(_msaglViewer);
        switch (valueMode)
        {
            case HighlightMode.EdgeHovered:
                _activeHighlighting = new EdgeHoveredHighlighting();
                break;
            case HighlightMode.OutgoingEdgesChildrenAndSelf:
                _activeHighlighting = new OutgointEdgesOfChildrenAndSelfHighlighting();
                break;
            case HighlightMode.ShortestNonSelfCircuit:
                _activeHighlighting = new HighligtShortestNonSelfCircuit();
                break;
            default:
                _activeHighlighting = new EdgeHoveredHighlighting();
                break;
        }
    }

    public void SetQuickInfoFactory(IQuickInfoFactory factory)
    {
        _factory = factory;
        _publisher.Publish(new QuickInfoUpdate(QuickInfoFactory.DefaultInfo));
    }


    public void ShowGlobalContextMenu()
    {
        // Click on free space
        if (_msaglViewer?.ObjectUnderMouseCursor != null ||
            _clonedCodeGraph.Nodes.Any() is false)
        {
            return;
        }

        var globalContextMenu = new ContextMenu();

        var item = new MenuItem { Header = "Complete dependencies" };
        item.Click += (_, _) => AddMissingDependencies();
        globalContextMenu.Items.Add(item);


        item = new MenuItem { Header = "Delete marked (with children)" };
        item.Click += (_, _) => DeleteAllMarkedElements();
        globalContextMenu.Items.Add(item);


        item = new MenuItem { Header = "Focus on marked elements" };
        item.Click += (_, _) => FocusOnMarkedElements();
        globalContextMenu.Items.Add(item);

        globalContextMenu.IsOpen = true;
    }

    private void DeleteAllMarkedElements()
    {
        if (_msaglViewer is null)
        {
            return;
        }

        PushUndo();

        var ids = _msaglViewer.Entities.Where(e => e.MarkedForDragging).OfType<IViewerNode>().Select(n => n.Node.Id);

        var idsToRemove = ids.ToHashSet();

        // All children
        foreach (var id in ids)
        {
            var children = _clonedCodeGraph.Nodes[id].GetChildrenIncludingSelf();
            idsToRemove.UnionWith(children);
        }

        _clonedCodeGraph.RemoveCodeElements(idsToRemove);     
        _presentationState.RemoveStates(idsToRemove);

        RefreshGraph();
    }

    private void FocusOnMarkedElements()
    {
        // We want to include all children of the collapsed code elements
        // and keep also the presentation state. Just less information

        if (_msaglViewer is null)
        {
            return;
        }

        var ids = _msaglViewer.Entities
            .Where(e => e.MarkedForDragging)
            .OfType<IViewerNode>().Select(n => n.Node.Id);

        if (ids.Any() is false)
        {
            return;
        }

        PushUndo();
        var idsToKeep = ids.ToHashSet();

        // All children
        foreach (var id in ids)
        {
            var children = _clonedCodeGraph.Nodes[id].GetChildrenIncludingSelf();
            idsToKeep.UnionWith(children);
        }

        var newGraph = _clonedCodeGraph.SubGraphOf(idsToKeep);

        // Cleanup unused states
        var idsToRemove = _clonedCodeGraph.Nodes.Keys.Except(idsToKeep).ToHashSet();
        _presentationState.RemoveStates(idsToRemove);

        _clonedCodeGraph = newGraph;
        RefreshGraph();
    }

    public bool Undo()
    {
        if (_undoStack.Any() is false)
        {
            return false;
        }

        var state = _undoStack.First();
        _undoStack.RemoveFirst();

        _clonedCodeGraph = state.CodeGraph;
        _presentationState = state.PresentationState;

        RefreshGraph();
        return true;
    }

    public void ImportCycleGroup(List<CodeElement> codeElements, List<Dependency> dependencies)
    {
        PushUndo();
        Clear(false);

        // Everything is collapsed by default. This allows to import large graphs.
        var defaultState = codeElements.Where(c => c.Children.Any()).ToDictionary(c => c.Id, c => true);
        _presentationState = new PresentationState(defaultState);
        AddToGraphInternal(codeElements, dependencies);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void PushUndo()
    {
        if (_undoStack.Count >= _undoStackSize)
        {
            // Make space
            _undoStack.RemoveLast();
        }

        var state = new UndoState(_clonedCodeGraph.Clone(null, null), _presentationState.Clone());
        _undoStack.AddFirst(state);
    }

    /// <summary>
    ///     Adds the new nodes, integrating hierarchical relationships from
    ///     original master nodes. Parent / child connections not present in this graph are discarded.
    ///     We may add them later when adding new elements.
    ///     The added element knows more about the original graph where it comes from than
    ///     the elements in the exploration graph. Some elements are not added yet.
    /// </summary>
    private void IntegrateNewFromOriginal(IEnumerable<CodeElement> originalCodeElements)
    {
        foreach (var originalElement in originalCodeElements)
        {
            _clonedCodeGraph.IntegrateCodeElementFromOriginal(originalElement);
        }
    }

    private void DeleteNode(Node node)
    {
        if (_msaglViewer is null)
        {
            return;
        }

        PushUndo();

        var element = (CodeElement)node.UserData;
        _clonedCodeGraph.RemoveCodeElement(element.Id);


        RefreshGraph();
    }

    private void RefreshGraph()
    {
        if (_msaglViewer != null)
        {
            var graph = _msaglBuilder.CreateGraphFromCodeStructure(_clonedCodeGraph, _presentationState,
                _showFlatGraph);

            _renderOption.Apply(graph);
            _msaglViewer.Graph = graph;
        }
    }


    IHighlighting _activeHighlighting = new EdgeHoveredHighlighting();

    private void ObjectUnderMouseCursorChanged(object? sender, ObjectUnderMouseCursorChangedEventArgs e)
    {
        if (_clickedObject is null)
        {
            // Update only if we don't hold the quick info.
            UpdateQuickInfoPanel(e.NewObject);
        }

        _activeHighlighting.Highlight(_msaglViewer, e.NewObject, _clonedCodeGraph);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void UpdateQuickInfoPanel(IViewerObject? obj)
    {
        var quickInfo = QuickInfoFactory.NoInfoProviderRegistered;
        if (_factory is not null)
        {
            quickInfo = _factory.CrateQuickInfo(obj);
        }

        _publisher.Publish(new QuickInfoUpdate(quickInfo));
    }

    private void Viewer_MouseDown(object sender, MsaglMouseEventArgs e)
    {
        bool IsShiftPressed()
        {
            return Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        }

        bool IsCtrlPressed()
        {
            return Keyboard.IsKeyDown(Key.LeftCtrl);
        }

        if (e.LeftButtonIsPressed)
        {
            var obj = _msaglViewer?.ObjectUnderMouseCursor;
            if (obj == null || (!IsShiftPressed() && !IsCtrlPressed()))
            {
                // Release the fixed info if we click on empty space or click 
                // anywhere without holding shift or ctrl.
                _clickedObject = null;
                UpdateQuickInfoPanel(obj);
                return;
            }

            if (IsCtrlPressed())
            {
                // User wants to "hold" the current quick info.
                _clickedObject = obj;
                UpdateQuickInfoPanel(obj);
                return;
            }
        }

        if (e.RightButtonIsPressed)
        {
            if (_msaglViewer?.ObjectUnderMouseCursor is not IViewerNode clickedObject)
            {
                return;
            }

            // Click on specific node
            var node = clickedObject.Node;
            var contextMenu = new ContextMenu();

            MenuItem item = null;
            if (node.UserData is CodeElement codeElement)
            {
                if (_presentationState.IsCollapsed(codeElement.Id) &&
                    codeElement.Children.Any())
                {
                    item = new MenuItem { Header = "Expand" };
                    item.Click += (_, _) => Expand(codeElement.Id);
                    contextMenu.Items.Add(item);
                }

                if (!_presentationState.IsCollapsed(codeElement.Id) &&
                    codeElement.Children.Any())
                {
                    item = new MenuItem { Header = "Collapse" };
                    item.Click += (_, _) => Collapse(codeElement.Id);
                    contextMenu.Items.Add(item);
                }
            }

            item = new MenuItem { Header = "Delete Node" };
            item.Click += (_, _) => DeleteNode(node);
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = "Find in Tree" };
            item.Click += (_, _) => FindInTree(node);
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = "Add parent" };
            item.Click += (_, _) => AddParentRequest(node);
            contextMenu.Items.Add(item);

            contextMenu.Items.Add(new Separator());
            var lastItemIsSeparator = true;

            // Dynamic parts
            if (node.UserData is CodeElement)
            {
                foreach (var cmd in _contextCommands)
                {
                    // Add separator command only if the last element was a real menu item.
                    if (cmd is SeparatorCommand)
                    {
                        if (lastItemIsSeparator is false)
                        {
                            contextMenu.Items.Add(new Separator());
                        }

                        continue;
                    }

                    if (!cmd.CanHandle(node.UserData))
                    {
                        continue;
                    }

                    var menuItem = new MenuItem { Header = cmd.Label };
                    menuItem.Click += (_, _) => cmd.Invoke(node.UserData);
                    contextMenu.Items.Add(menuItem);
                    lastItemIsSeparator = false;
                }
            }

            contextMenu.IsOpen = true;
        }
        else
        {
            e.Handled = false;
        }
    }

    private void Collapse(string id)
    {
        PushUndo();
        _presentationState.SetCollapsedState(id, true);
        RefreshGraph();
    }

    private void Expand(string id)
    {
        PushUndo();
        _presentationState.SetCollapsedState(id, false);

        RefreshGraph();
    }



    private void AddMissingDependencies()
    {
        PushUndo();

        // We do not know the original graph.
        _publisher.Publish(new AddMissingDependenciesRequest());
    }

    private void AddParentRequest(Node node)
    {
        PushUndo();

        // We do not know the original graph.
        _publisher.Publish(new AddParentContainerRequest(node.Id));
    }

    private void FindInTree(Node node)
    {
        _publisher.Publish(new LocateInTreeRequest(node.Id));
    }
}