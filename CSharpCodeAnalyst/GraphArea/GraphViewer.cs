using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.GraphArea.Highlighting;
using CSharpCodeAnalyst.GraphArea.RenderOptions;
using CSharpCodeAnalyst.Help;
using Microsoft.Msagl.Drawing;
using Node = Microsoft.Msagl.Drawing.Node;

namespace CSharpCodeAnalyst.GraphArea;

/// <summary>
///     Note:
///     Between nodes we can have multiple relationships if the relationship type is different.
///     Relationships of the same type (i.e a method Calls another multiple times) are handled
///     in the parser. In this case the relationship holds all source references.
///     If ever the MSAGL is replaced this is the adapter to re-write.
/// </summary>
internal class GraphViewer : IGraphViewer, IGraphBinding, INotifyPropertyChanged
{
    private readonly List<IRelationshipContextCommand> _edgeCommands = [];
    private readonly List<IGlobalContextCommand> _globalCommands = [];
    private readonly MsaglBuilder _msaglBuilder;
    private readonly List<ICodeElementContextCommand> _nodeCommands = [];
    private readonly IPublisher _publisher;

    private IHighlighting _activeHighlighting = new EdgeHoveredHighlighting();

    /// <summary>
    ///     Held to read the help
    /// </summary>
    private IViewerObject? _clickedObject;

    private CodeGraph _clonedCodeGraph = new();
    private IQuickInfoFactory? _factory;
    private Microsoft.Msagl.WpfGraphControl.GraphViewer? _msaglViewer;
    private PresentationState _presentationState = new();
    private RenderOption _renderOption = new DefaultRenderOptions();
    private bool _showFlatGraph;

    /// <summary>
    ///     Note:
    ///     Between nodes we can have multiple relationships if the relationship type is different.
    ///     Relationships of the same type (i.e a method Calls another multiple times) are handled
    ///     in the parser. In this case the relationship holds all source references.
    /// </summary>
    public GraphViewer(IPublisher publisher)
    {
        _publisher = publisher;
        _msaglBuilder = new MsaglBuilder();
        SetHighlightMode(HighlightMode.EdgeHovered);
    }

    public void Bind(Panel graphPanel)
    {
        _msaglViewer = new Microsoft.Msagl.WpfGraphControl.GraphViewer();
        _msaglViewer.BindToPanel(graphPanel);

        _msaglViewer.ObjectUnderMouseCursorChanged += ObjectUnderMouseCursorChanged;
        _msaglViewer.MouseDown += Viewer_MouseDown!;
    }

    public void ShowFlatGraph(bool value)
    {
        _showFlatGraph = value;
        RefreshGraph();
    }

    /// <summary>
    ///     Adding an existing element or relationship is prevented.
    ///     Note from the originalCodeElement we don't add parent or children.
    ///     We just use this information to integrate the node into the existing canvas.
    /// </summary>
    public void AddToGraph(IEnumerable<CodeElement> originalCodeElements, IEnumerable<Relationship> newRelationships)
    {
        if (!IsBoundToPanel())
        {
            return;
        }

        AddToGraphInternal(originalCodeElements, newRelationships);
        RefreshGraph();
    }

    public void AddContextMenuCommand(ICodeElementContextCommand command)
    {
        _nodeCommands.Add(command);
    }

    public void AddContextMenuCommand(IRelationshipContextCommand command)
    {
        _edgeCommands.Add(command);
    }

    public void AddGlobalContextMenuCommand(IGlobalContextCommand command)
    {
        _globalCommands.Add(command);
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
        _activeHighlighting.Clear(_msaglViewer);
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

        var markedElements = GetMarkedElementIds()
            .Select(id => _clonedCodeGraph.Nodes[id])
            .ToList();

        foreach (var command in _globalCommands)
        {
            if (command.CanHandle(markedElements) is false)
            {
                continue;
            }

            var menuItem = new MenuItem { Header = command.Label };
            menuItem.Click += (_, _) => command.Invoke(markedElements);
            globalContextMenu.Items.Add(menuItem);
        }

        globalContextMenu.IsOpen = true;
    }

    public GraphSession GetSession()
    {
        return GraphSession.Create("", _clonedCodeGraph, _presentationState);
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

    public void DeleteFromGraph(List<Relationship> relationships)
    {
        if (_msaglViewer is null)
        {
            return;
        }

        foreach (var relationship in relationships)
        {
            _clonedCodeGraph.Nodes[relationship.SourceId].Relationships.Remove(relationship);
        }

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

        _clonedCodeGraph.RemoveCodeElements(idsToRemove);
        _presentationState.RemoveStates(idsToRemove);

        RefreshGraph();
    }

    public void Collapse(string id)
    {
        _presentationState.SetCollapsedState(id, true);
        RefreshGraph();
    }

    public void Expand(string id)
    {
        _presentationState.SetCollapsedState(id, false);
        RefreshGraph();
    }

    public bool IsCollapsed(string id)
    {
        return _presentationState.IsCollapsed(id);
    }

    public void LoadSession(List<CodeElement> codeElements, List<Relationship> relationships, PresentationState state)
    {
        if (_msaglViewer is null)
        {
            return;
        }

        Clear();
        AddToGraphInternal(codeElements, relationships);
        _presentationState = state;

        RefreshGraph();
    }

    public void LoadSession(CodeGraph newGraph, PresentationState? presentationState)
    {
        if (presentationState is null)
        {
            presentationState = new PresentationState();
        }

        _presentationState = presentationState;
        _clonedCodeGraph = newGraph;
        RefreshGraph();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool IsBoundToPanel()
    {
        return _msaglViewer is not null;
    }

    private void AddToGraphInternal(IEnumerable<CodeElement> originalCodeElements,
        IEnumerable<Relationship> newRelationships)
    {
        if (_msaglViewer is null)
        {
            return;
        }

        IntegrateNewFromOriginal(originalCodeElements);

        // Add relationships we explicitly requested.
        foreach (var newRelationship in newRelationships)
        {
            var sourceElement = _clonedCodeGraph.Nodes[newRelationship.SourceId];
            sourceElement.Relationships.Add(newRelationship);
        }
    }

    private HashSet<string> GetMarkedElementIds()
    {
        if (_msaglViewer is null)
        {
            return [];
        }

        var markedIds = _msaglViewer.Entities
            .Where(e => e.MarkedForDragging)
            .OfType<IViewerNode>()
            .Select(n => n.Node.Id)
            .ToHashSet();
        return markedIds;
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
            var graph = _msaglBuilder.CreateGraph(_clonedCodeGraph, _presentationState,
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
            if (_msaglViewer?.ObjectUnderMouseCursor is IViewerNode clickedObject)
            {
                // Click on specific node
                var node = clickedObject.Node;
                var contextMenu = new ContextMenu();
                var element = GetCodeElementFromUserData(node);
                AddToContextMenuEntries(element, contextMenu);
                if (contextMenu.Items.Count > 0)
                {
                    contextMenu.IsOpen = true;
                }
            }
            else if (_msaglViewer?.ObjectUnderMouseCursor is IViewerEdge viewerEdge)
            {
                // Click on specific edge
                var edge = viewerEdge.Edge;
                var contextMenu = new ContextMenu();
                var relationships = GetRelationshipsFromUserData(edge);
                AddContextMenuEntries(relationships, contextMenu);
                if (contextMenu.Items.Count > 0)
                {
                    contextMenu.IsOpen = true;
                }
            }
            else
            {
                // Click on free space
                ShowGlobalContextMenu();
            }
        }
        else
        {
            e.Handled = false;
        }
    }

    private void AddContextMenuEntries(List<Relationship> relationships, ContextMenu contextMenu)
    {
        if (relationships.Count == 0)
        {
            return;
        }

        foreach (var cmd in _edgeCommands)
        {
            var menuItem = new MenuItem { Header = cmd.Label };
            if (cmd.CanHandle(relationships))
            {
                menuItem.Click += (_, _) => cmd.Invoke(relationships);
                contextMenu.Items.Add(menuItem);
            }
        }
    }

    private static List<Relationship> GetRelationshipsFromUserData(Edge edge)
    {
        var result = new List<Relationship>();
        switch (edge.UserData)
        {
            case Relationship relationship:
                result.Add(relationship);
                break;
            case List<Relationship> relationships:
                result.AddRange(relationships);
                break;
        }

        return result;
    }

    private CodeElement? GetCodeElementFromUserData(Node node)
    {
        return node.UserData as CodeElement;
    }

    /// <summary>
    ///     Commands registered for nodes
    /// </summary>
    private void AddToContextMenuEntries(CodeElement? element, ContextMenu contextMenu)
    {
        if (element is null)
        {
            return;
        }

        var lastItemIsSeparator = true;

        foreach (var cmd in _nodeCommands)
        {
            // Add separator command only if the last element was a real menu item.
            if (cmd is SeparatorCommand)
            {
                if (lastItemIsSeparator is false)
                {
                    contextMenu.Items.Add(new Separator());
                    lastItemIsSeparator = true;
                }

                continue;
            }

            if (!cmd.CanHandle(element))
            {
                continue;
            }

            var menuItem = new MenuItem { Header = cmd.Label };
            menuItem.Click += (_, _) => cmd.Invoke(element);
            contextMenu.Items.Add(menuItem);
            lastItemIsSeparator = false;
        }
    }
}