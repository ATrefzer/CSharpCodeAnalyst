using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using CodeParser.Extensions;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.GraphArea.Highlighig;
using CSharpCodeAnalyst.GraphArea.RenderOptions;
using CSharpCodeAnalyst.Help;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;
using Node = Microsoft.Msagl.Drawing.Node;


namespace CSharpCodeAnalyst.GraphArea;

/// <summary>
///     Note:
///     Between nodes we can have multiple dependencies if the dependency type is different.
///     Dependencies of the same type (i.e a method Calls another multiple times) are handled
///     in the parser. In this case the dependency holds all source references.
/// </summary>
internal class DependencyGraphViewer : IDependencyGraphViewer, IDependencyGraphBinding, INotifyPropertyChanged
{
    private readonly List<IContextCommand> _dynamicContextCommands = [];
    private readonly MsaglBuilder _msaglBuilder;
    private readonly IPublisher _publisher;
    private readonly List<IContextCommand> _staticContextCommands = [];

    private IHighlighting _activeHighlighting = new EdgeHoveredHighlighting();

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

    public event EventHandler? BeforeChange;

    public void ShowFlatGraph(bool value)
    {
        _showFlatGraph = value;
        RefreshGraph();
    }

    /// <summary>
    ///     Adding an existing element or dependency is prevented.
    ///     Note from the originalCodeElement we don't add parent or children.
    ///     We just use this information to integrate the node into the existing canvas.
    /// </summary>
    public void AddToGraph(IEnumerable<CodeElement> originalCodeElements, IEnumerable<Dependency> newDependencies)
    {
        if (!IsBoundToPanel())
        {
            return;
        }

        RaiseBeforeChange();
        AddToGraphInternal(originalCodeElements, newDependencies);
        RefreshGraph();
    }

    public void AddDynamicContextCommand(IContextCommand command)
    {
        _dynamicContextCommands.Add(command);
    }

    public void AddStaticContextCommand(IContextCommand command)
    {
        _staticContextCommands.Add(command);
    }

    public void Layout()
    {
        //_msaglViewer?.SetInitialTransform();
        RefreshGraph();
    }

    public CodeGraph GetGraph()
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


    public void RestoreSession(List<CodeElement> codeElements, List<Dependency> dependencies, PresentationState state)
    {
        if (_msaglViewer is null)
        {
            return;
        }

        RaiseBeforeChange();
        Clear();

        AddToGraphInternal(codeElements, dependencies);
        _presentationState = state;

        RefreshGraph();
    }

    public void ImportCycleGroup(List<CodeElement> codeElements, List<Dependency> dependencies)
    {
        if (_msaglViewer is null)
        {
            return;
        }

        RaiseBeforeChange();
        Clear();

        // Everything is collapsed by default. This allows to import large graphs.
        var defaultState = codeElements.Where(c => c.Children.Any()).ToDictionary(c => c.Id, c => true);
        _presentationState = new PresentationState(defaultState);
        AddToGraphInternal(codeElements, dependencies);

        var roots = _clonedCodeGraph.GetRoots();
        if (roots.Count == 1)
        {
            // Usability. If we have a single root, we expand it.
            _presentationState.SetCollapsedState(roots[0].Id, false);
        }

        RefreshGraph();
    }

    public GraphSessionState GetSessionState()
    {
        return GraphSessionState.Create("", _clonedCodeGraph, _presentationState);
    }


    public void Clear()
    {
        if (_msaglViewer is null)
        {
            return;
        }

        _clonedCodeGraph = new CodeGraph();

        // Nothing collapsed by default
        _presentationState = new PresentationState();
        RefreshGraph();
    }

    public void DeleteFromGraph(HashSet<string> idsToRemove)
    {
        if (_msaglViewer is null)
        {
            return;
        }

        if (idsToRemove.Any() is false)
        {
            return;
        }

        RaiseBeforeChange();

        _clonedCodeGraph.RemoveCodeElements(idsToRemove);
        _presentationState.RemoveStates(idsToRemove);

        RefreshGraph();
    }

    public void Collapse(string id)
    {
        RaiseBeforeChange();
        _presentationState.SetCollapsedState(id, true);
        RefreshGraph();
    }

    public void Expand(string id)
    {
        RaiseBeforeChange();
        _presentationState.SetCollapsedState(id, false);
        RefreshGraph();
    }

    public bool IsCollapsed(string id)
    {
        return _presentationState.IsCollapsed(id);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool IsBoundToPanel()
    {
        return _msaglViewer is not null;
    }

    private void AddToGraphInternal(IEnumerable<CodeElement> originalCodeElements,
        IEnumerable<Dependency> newDependencies)
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
    }


    private void DeleteAllMarkedElements()
    {
        if (_msaglViewer is null)
        {
            return;
        }

        var markedIds = _msaglViewer.Entities
            .Where(e => e.MarkedForDragging)
            .OfType<IViewerNode>()
            .Select(n => n.Node.Id)
            .ToHashSet();


        DeleteFromGraph(markedIds, true);
    }


    private void DeleteFromGraph(HashSet<string> ids, bool withChildren)
    {
        var idsToRemove = ids.ToHashSet();

        // Include children
        if (withChildren)
        {
            foreach (var id in ids)
            {
                var children = _clonedCodeGraph.Nodes[id].GetChildrenIncludingSelf();
                idsToRemove.UnionWith(children);
            }
        }

        DeleteFromGraph(idsToRemove);
    }

    private void RaiseBeforeChange()
    {
        BeforeChange?.Invoke(this, EventArgs.Empty);
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
            .OfType<IViewerNode>().Select(n => n.Node.Id).ToList();

        if (ids.Any() is false)
        {
            return;
        }

        RaiseBeforeChange();
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


    /// <summary>
    ///     Adds the new nodes, integrating hierarchical relationships from
    ///     original master nodes. Parent / child connections not present in this graph are discarded.
    ///     We may add them later when adding new elements.
    ///     The original elements get cloned.
    /// </summary>
    private void IntegrateNewFromOriginal(IEnumerable<CodeElement> originalCodeElements)
    {
        foreach (var originalElement in originalCodeElements)
        {
            _clonedCodeGraph.IntegrateCodeElementFromOriginal(originalElement);
        }
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

            AddToContextMenuStaticEntries(node, contextMenu);
            AddToContextMenuDynamicEntries(node, contextMenu);

            contextMenu.IsOpen = true;
        }
        else
        {
            e.Handled = false;
        }
    }

    private void AddToContextMenuStaticEntries(Node node, ContextMenu contextMenu)
    {
        foreach (var cmd in _staticContextCommands)
        {
            if (cmd.CanHandle(node.UserData))
            {
                var menuItem = new MenuItem { Header = cmd.Label };
                menuItem.Click += (_, _) => cmd.Invoke(node.UserData);
                contextMenu.Items.Add(menuItem);
            }
        }
    }

    /// <summary>
    ///     Commands registered for type of nodes
    /// </summary>
    private void AddToContextMenuDynamicEntries(Node node, ContextMenu contextMenu)
    {
        contextMenu.Items.Add(new Separator());
        var lastItemIsSeparator = true;

        if (node.UserData is CodeElement)
        {
            foreach (var cmd in _dynamicContextCommands)
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
    }

    private void AddMissingDependencies()
    {
        // We do not know the original graph.
        _publisher.Publish(new AddMissingDependenciesRequest());
    }
}