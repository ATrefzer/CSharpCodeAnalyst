using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Features.Graph.Filtering;
using CSharpCodeAnalyst.Features.Graph.Highlighting;
using CSharpCodeAnalyst.Features.Graph.RenderOptions;
using CSharpCodeAnalyst.Features.Help;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messages;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Drawing;
using Node = Microsoft.Msagl.Drawing.Node;

namespace CSharpCodeAnalyst.Features.Graph;

/// <summary>
///     Note:
///     Between nodes we can have multiple relationships if the relationship type is different.
///     Relationships of the same type (i.e a method Calls another multiple times) are handled
///     in the parser. In this case the relationship holds all source references.
///     If ever the MSAGL is replaced this is the adapter to re-write.
/// </summary>
public class GraphViewer : IGraphViewer, IGraphBinding, INotifyPropertyChanged, IGraphViewerHighlighting
{
    private readonly int _maxElementWarningLimit;
    private readonly IPublisher _publisher;

    /// <summary>
    ///     The render-agnostic model. GraphViewer is now the MSAGL render adapter over it:
    ///     state operations are delegated to <see cref="_state" />, and the private members
    ///     below are read-only forwarders so the existing rendering code keeps working.
    /// </summary>
    private readonly GraphViewState _state;

    private IHighlighting _activeHighlighting = new EdgeHoveredHighlighting();

    private ClickController? _clickController;

    /// <summary>
    ///     Held to read the help
    /// </summary>
    private IViewerObject? _clickedObject;

    private IQuickInfoFactory? _factory;
    private Microsoft.Msagl.WpfGraphControl.GraphViewer? _msaglViewer;
    private RenderOption _renderOption = new DefaultRenderOptions();

    // Forwarders to the model so existing read sites stay unchanged.
    private CodeGraph.Graph.CodeGraph _clonedCodeGraph => _state.CodeGraph;
    private PresentationState _presentationState => _state.PresentationState;
    private GraphHideFilter _hideFilter => _state.HideFilter;
    private bool _flow => _state.ShowInformationFlow;
    private bool _showFlatGraph => _state.ShowFlat;
    private HighlightMode _highlightMode => _state.HighlightMode;
    private IReadOnlyList<ICodeElementContextCommand> _nodeCommands => _state.NodeCommands;
    private IReadOnlyList<IRelationshipContextCommand> _edgeCommands => _state.EdgeCommands;
    private IReadOnlyList<IGlobalCommand> _globalCommands => _state.GlobalCommands;

    /// <summary>
    ///     Note:
    ///     Between nodes we can have multiple relationships if the relationship type is different.
    ///     Relationships of the same type (i.e a method Calls another multiple times) are handled
    ///     in the parser. In this case the relationship holds all source references.
    /// </summary>
    public GraphViewer(IPublisher publisher, int settings, GraphViewState state)
    {
        _publisher = publisher;
        _maxElementWarningLimit = settings;
        _state = state;

        // GraphViewer renders and reacts to the shared model.
        _state.Changed += OnStateChanged;
        _state.HighlightModeChanged += OnStateHighlightModeChanged;
        _state.SetHighlightMode(HighlightMode.EdgeHovered);
    }

    private void OnStateChanged()
    {
        // A structural change in the model: re-render and notify other observers.
        RefreshGraph();
        OnGraphChanged();
    }

    private void OnStateHighlightModeChanged(HighlightMode mode)
    {
        ClearAllEdgeHighlighting();
        _activeHighlighting = mode switch
        {
            HighlightMode.EdgeHovered => new EdgeHoveredHighlighting(),
            HighlightMode.OutgoingEdgesChildrenAndSelf => new OutgoingEdgesOfChildrenAndSelfHighlighting(),
            HighlightMode.ShortestNonSelfCircuit => new HighlightShortestNonSelfCircuit(),
            _ => new EdgeHoveredHighlighting()
        };

        // Forward to IGraphViewer subscribers (e.g. the web view).
        HighlightModeChanged?.Invoke(mode);
    }

    public void Bind(Panel graphPanel)
    {
        _msaglViewer = new Microsoft.Msagl.WpfGraphControl.GraphViewer();
        _msaglViewer.BindToPanel(graphPanel);

        _msaglViewer.ObjectUnderMouseCursorChanged += ObjectUnderMouseCursorChanged;

        _clickController = new ClickController(_msaglViewer);

        _clickController.LeftDoubleClick += OnLeftDoubleClick;
        _clickController.LeftSingleClick += OnLeftSingleClick;
        _clickController.OpenContextMenu += OnOpenContextMenu;

        // Feed MSAGL's selection (marquee / click, settled by mouse-up) into the shared
        // model so selection-based commands read one canonical source. The state ignores
        // no-op updates, so panning / right-click that don't change selection are cheap.
        _msaglViewer.MouseUp += (_, _) => _state.SetSelection(GetSelectedElementIds());
    }


    public void ShowFlatGraph(bool value)
    {
        _state.SetShowFlat(value);
    }

    public void ShowInformationFlow(bool value)
    {
        _state.SetShowInformationFlow(value);
    }

    /// <summary>
    ///     Adding an existing element or relationship is prevented.
    ///     Note from the originalCodeElement we don't add parent or children.
    ///     We just use this information to integrate the node into the existing canvas.
    /// </summary>
    public void AddToGraph(IEnumerable<CodeElement> originalCodeElements, IEnumerable<Relationship> newRelationships, bool addCollapsed)
    {
        _state.AddToGraph(originalCodeElements, newRelationships, addCollapsed);
    }

    public void AddCommand(ICodeElementContextCommand command)
    {
        _state.AddCommand(command);
    }

    public void AddCommand(IRelationshipContextCommand command)
    {
        _state.AddCommand(command);
    }

    public void AddGlobalCommand(IGlobalCommand command)
    {
        _state.AddGlobalCommand(command);
    }

    public IReadOnlyList<ICodeElementContextCommand> GetNodeContextCommands()
    {
        return _nodeCommands;
    }

    public IReadOnlyList<IRelationshipContextCommand> GetEdgeContextCommands()
    {
        return _edgeCommands;
    }

    public IReadOnlyList<IGlobalCommand> GetGlobalContextCommands()
    {
        return _globalCommands;
    }

    public void Layout()
    {
        //_msaglViewer?.SetInitialTransform();
        RefreshGraph();
    }

    public CodeGraph.Graph.CodeGraph GetGraph()
    {
        return _clonedCodeGraph;
    }

    public void UpdateRenderOption(RenderOption renderOption)
    {
        _renderOption = renderOption;
        RefreshGraph();
    }

    public void SetHideFilter(GraphHideFilter filter)
    {
        _state.SetHideFilter(filter);
    }

    public GraphHideFilter GetHideFilter()
    {
        return _hideFilter;
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

    public HighlightMode GetHighlightMode()
    {
        return _highlightMode;
    }

    public event Action<HighlightMode>? HighlightModeChanged;

    public void SetHighlightMode(HighlightMode valueMode)
    {
        _state.SetHighlightMode(valueMode);
    }

    public void SetQuickInfoFactory(IQuickInfoFactory factory)
    {
        _factory = factory;
        _publisher.Publish(new QuickInfoUpdateRequest(QuickInfoFactory.DefaultInfo));
    }


    public void ShowGlobalContextMenu()
    {
        // Click on free space
        if (!_globalCommands.Any() || _msaglViewer?.ObjectUnderMouseCursor != null ||
            !_clonedCodeGraph.Nodes.Any())
        {
            return;
        }

        var globalContextMenu = new ContextMenu();

        var selectedElements = GetSelectedElementIds()
            .Select(id => _clonedCodeGraph.Nodes[id])
            .ToList();

        foreach (var command in _globalCommands)
        {
            if (!command.CanHandle(selectedElements))
            {
                continue;
            }

            var menuItem = new MenuItem { Header = command.Label };

            // Add icon if provided
            if (command.Icon != null)
            {
                var iconImage = new Image
                {
                    Width = 16,
                    Height = 16,
                    Source = command.Icon
                };
                menuItem.Icon = iconImage;
            }

            menuItem.Click += (_, _) => command.Invoke(selectedElements);
            globalContextMenu.Items.Add(menuItem);
        }

        globalContextMenu.IsOpen = true;
    }

    public GraphSession GetSession()
    {
        return _state.GetSession();
    }


    public void Clear()
    {
        _state.Clear();
    }

    public void RemoveFromGraph(List<Relationship> relationships)
    {
        _state.RemoveRelationships(relationships);
    }

    public void RemoveFromGraph(HashSet<string> idsToRemove)
    {
        _state.RemoveElements(idsToRemove);
    }

    public void Collapse(string id)
    {
        _state.Collapse(id);
    }

    public void Expand(string id)
    {
        _state.Expand(id);
    }

    public bool IsCollapsed(string id)
    {
        return _presentationState.IsCollapsed(id);
    }

    public bool IsFlagged(string id)
    {
        return _state.IsFlagged(id);
    }

    // The data mutation goes through the shared state (which raises DecorationsChanged so the
    // web view restyles too); the MSAGL-specific no-layout refresh stays here.
    public void ToggleFlag(string id)
    {
        _state.ToggleFlag(id);
        RefreshNodeDecorationWithoutLayout([id]);
    }

    public void ToggleFlag(string sourceId, string targetId, List<Relationship> relationships)
    {
        _state.ToggleFlag(sourceId, targetId);
        RefreshEdgeDecorationWithoutLayout([(sourceId, targetId)]);
    }

    public void ClearAllFlags()
    {
        var affectedIds = _presentationState.NodeIdToFlagged.Keys.ToList();
        _state.ClearAllFlags();
        RefreshNodeDecorationWithoutLayout(affectedIds);

        // After the highlighting is removed the state appears.
        ClearAllEdgeHighlighting();
    }

    public void SetSearchHighlights(List<string> nodeIds)
    {
        var previousIds = _presentationState.NodeIdToSearchHighlighted.Keys.ToList();
        _state.SetSearchHighlights(nodeIds);

        // Refresh the union of previously and newly highlighted nodes.
        var allAffectedIds = previousIds.Union(nodeIds).ToList();
        RefreshNodeDecorationWithoutLayout(allAffectedIds);
    }

    public void ClearSearchHighlights()
    {
        var ids = _presentationState.NodeIdToSearchHighlighted.Keys.ToList();
        _state.ClearSearchHighlights();
        RefreshNodeDecorationWithoutLayout(ids);
    }

    public void LoadSession(List<CodeElement> codeElements, List<Relationship> relationships, PresentationState state)
    {
        _state.LoadSession(codeElements, relationships, state);
    }

    public void LoadSession(CodeGraph.Graph.CodeGraph newGraph, PresentationState? presentationState)
    {
        _state.LoadSession(newGraph, presentationState);
    }

    public event Action<CodeGraph.Graph.CodeGraph>? GraphChanged;

    public bool TryHandleKeyEvent(Key key)
    {
        var cmd = _globalCommands.FirstOrDefault(c => c.Key == key);
        if (cmd is null)
        {
            return false;
        }

        var selectedElements = GetSelectedElementIds()
            .Select(id => _clonedCodeGraph.Nodes[id])
            .ToList();
        if (cmd.CanHandle(selectedElements))
        {
            cmd.Invoke(selectedElements);
            return true;
        }

        return false;
    }

    public HashSet<string> GetSelectedElementIds()
    {
        if (_msaglViewer is null)
        {
            return [];
        }

        var selectedIds = _msaglViewer.Entities
            .Where(e => e.MarkedForDragging)
            .OfType<IViewerNode>()
            .Select(n => n.Node.Id)
            .ToHashSet();
        return selectedIds;
    }

    public void ClearQuickInfo()
    {
        _clickedObject = null;
    }

    public Microsoft.Msagl.WpfGraphControl.GraphViewer? GetMsaglGraphViewer()
    {
        return _msaglViewer;
    }

    public void ClearAllEdgeHighlighting()
    {
        if (_msaglViewer is null)
        {
            return;
        }

        var edges = _msaglViewer.Entities.OfType<IViewerEdge>();
        foreach (var edge in edges)
        {
            ClearEdgeHighlighting(edge);
        }
    }

    /// <summary>
    ///     <inheritdoc cref="IGraphViewerHighlighting.ClearAllEdgeHighlighting" />
    /// </summary>
    public void ClearEdgeHighlighting(IViewerEdge? edge)
    {
        if (edge is null)
        {
            return;
        }

        edge.Edge.Attr.Color = Constants.DefaultLineColor;
        edge.Edge.Attr.LineWidth = Constants.DefaultLineWidth;

        if (edge.Edge.UserData is Relationship { Type: RelationshipType.Containment })
        {
            edge.Edge.Attr.Color = Constants.GrayColor;
        }

        // Clearing highlighting recovers the flagged state.
        if (_presentationState.IsFlagged((edge.Edge.Source, edge.Edge.Target)))
        {
            edge.Edge.Attr.LineWidth = Constants.FlagLineWidth;
            edge.Edge.Attr.Color = Constants.FlagColor;
        }
    }

    public void HighlightEdge(IViewerEdge edge)
    {
        edge.Edge.Attr.Color = Constants.MouseHighlightColor;
        edge.Edge.Attr.LineWidth = Constants.MouseHighlightLineWidth;
        _msaglViewer?.Invalidate(edge);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnGraphChanged()
    {
        GraphChanged?.Invoke(_clonedCodeGraph);
    }

    private void OnOpenContextMenu(IViewerObject? obj)
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
            var sourceId = edge.Source;
            var targetid = edge.Target;
            var contextMenu = new ContextMenu();
            var relationships = GetRelationshipsFromUserData(edge);
            AddContextMenuEntries(sourceId, targetid, relationships, contextMenu);
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

    private void OnLeftSingleClick(IViewerObject? obj)
    {
        if (obj == null)
        {
            // Release the fixed info if we click on empty space
            _clickedObject = null;
        }
        else
        {
            // User wants to "hold" the current quick info.
            _clickedObject = obj;
        }

        UpdateQuickInfoPanel(obj);
    }

    private void OnLeftDoubleClick(IViewerObject? obj)
    {
        // Execute double click action if any registered
        if (obj is IViewerNode clickedObject)
        {
            var node = clickedObject.Node;
            var element = GetCodeElementFromUserData(node);
            if (element is not null)
            {
                foreach (var cmd in _nodeCommands)
                {
                    if (cmd.IsDoubleClickable && cmd.CanHandle(element))
                    {
                        cmd.Invoke(element);
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Assumption. In the hierarchical view there is only one edge connecting two nodes. Real edges are bundled together.
    /// </summary>
    private void RefreshEdgeDecorationWithoutLayout(List<(string sourceId, string targetId)> relationships)
    {
        if (_msaglViewer?.Graph is null)
        {
            return;
        }

        //var edges = _msaglViewer.Graph.Edges.Where(e => relationships.Contains((e.Source, e.Target)));
        var edges = _msaglViewer.Entities.OfType<IViewerEdge>().Where(e => relationships.Contains((e.Edge.Source, e.Edge.Target)));

        foreach (var edge in edges)
        {
            ClearEdgeHighlighting(edge);
        }
    }

    private void RefreshNodeDecorationWithoutLayout(List<string> ids)
    {
        if (_msaglViewer?.Graph is null)
        {
            return;
        }

        foreach (var id in ids)
        {
            if (!TryGetNodeOrSubGraphToRefresh(id, out var node))
            {
                continue;
            }

            if (node is null)
            {
                continue;
            }

            // Apply correct styling based on current state
            if (_presentationState.IsFlagged(id))
            {
                // Flagged takes precedence over search highlight.
                // We don't want to destroy the flags when updating highlights.
                node.Attr.Color = Constants.FlagColor;
                node.Attr.LineWidth = Constants.FlagLineWidth;
            }
            else if (_presentationState.IsSearchHighlighted(id))
            {
                node.Attr.Color = Constants.SearchHighlightColor;
                node.Attr.LineWidth = Constants.SearchHighlightLineWidth;
            }
            else
            {
                node.Attr.Color = Constants.DefaultLineColor;
                node.Attr.LineWidth = Constants.DefaultLineWidth;
            }
        }
    }

    private bool TryGetNodeOrSubGraphToRefresh(string id, out Node? node)
    {
        node = _msaglViewer!.Graph.FindNode(id);
        if (node is not null)
        {
            return true;
        }

        // If the id is rendered as an expanded subgraph. Both derive from Node.
        if (_msaglViewer.Graph.SubgraphMap.TryGetValue(id, out var subGraph))
        {
            node = subGraph;
            return true;
        }

        // This happens when the highlighting finds matches that are not currently visible.
        return false;
    }

    private bool ShouldProceedWithLargeGraph(int numberOfElements)
    {
        if (numberOfElements > _maxElementWarningLimit)
        {
            var msg = string.Format(Strings.TooMuchElementsMessage, numberOfElements);
            var title = Strings.TooMuchElementsTitle;
            var result = MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return MessageBoxResult.Yes == result;
        }

        return true;
    }

    private bool RefreshGraph(bool askUserToShowLargeGraphs = true)
    {
        try
        {
            if (_msaglViewer != null)
            {
                MsaglBuilderBase builder = _showFlatGraph ? new MsaglFlatBuilder() : new MsaglHierarchicalBuilder();
                var graph = builder.CreateGraph(_clonedCodeGraph, _presentationState, _flow, _hideFilter);

                if (askUserToShowLargeGraphs)
                {
                    var elements = graph.Nodes.Count() + graph.Edges.Count() + graph.SubgraphMap.Count;
                    if (!ShouldProceedWithLargeGraph(elements))
                    {
                        _msaglViewer.Graph = new Microsoft.Msagl.Drawing.Graph();
                        return false;
                    }
                }


                _renderOption.Apply(graph);

                Exception? renderException = null;
                try
                {
                    _msaglViewer.Graph = graph;
                }
                catch (Exception ex)
                {
                    renderException = ex;
                }

                if (renderException != null)
                {
                    // In rare cases the rendering fails when finding attachment points for bundled edges.
                    // As a workaround I render the graph with straight lines.
                    MessageBox.Show(Strings.GraphRenderingWarning, Strings.Warning_Title, MessageBoxButton.OK, MessageBoxImage.Warning);

                    var settings = graph.LayoutAlgorithmSettings;
                    settings.EdgeRoutingSettings.EdgeRoutingMode = EdgeRoutingMode.StraightLine;
                    _msaglViewer.Graph = graph;
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);
            MessageBox.Show(Strings.GraphRenderingError, Strings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return true;
    }


    private void ObjectUnderMouseCursorChanged(object? sender, ObjectUnderMouseCursorChangedEventArgs e)
    {
        if (_clickedObject is null)
        {
            // Update only if we don't hold the quick info.
            UpdateQuickInfoPanel(e.NewObject);
        }

        ClearAllEdgeHighlighting();
        _activeHighlighting.Highlight(this, e.NewObject, _clonedCodeGraph);
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

        _publisher.Publish(new QuickInfoUpdateRequest(quickInfo));
    }

    private void AddContextMenuEntries(string sourceId, string targetId, List<Relationship> relationships, ContextMenu contextMenu)
    {
        if (relationships.Count == 0)
        {
            return;
        }

        Dictionary<string, MenuItem> subMenus = [];
        foreach (var cmd in _edgeCommands)
        {
            if (cmd.CanHandle(relationships))
            {
                MenuItem? parentMenu = null;
                var menuItem = new MenuItem { Header = cmd.Label };

                var isSubMenu = !string.IsNullOrEmpty(cmd.SubMenuGroup);
                if (isSubMenu && cmd.SubMenuGroup != null)
                {
                    if (!subMenus.TryGetValue(cmd.SubMenuGroup, out parentMenu))
                    {
                        parentMenu = new MenuItem();
                        parentMenu.Header = cmd.SubMenuGroup;
                        subMenus[cmd.SubMenuGroup] = parentMenu;
                        contextMenu.Items.Add(parentMenu);
                    }
                }

                // Add icon if provided
                if (cmd.Icon != null)
                {
                    var iconImage = new Image
                    {
                        Width = 16,
                        Height = 16,
                        Source = cmd.Icon
                    };
                    menuItem.Icon = iconImage;
                }

                menuItem.Click += (_, _) => cmd.Invoke(sourceId, targetId, relationships);

                if (!isSubMenu)
                {
                    contextMenu.Items.Add(menuItem);
                }
                else
                {
                    parentMenu!.Items.Add(menuItem);
                }
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

    private static CodeElement? GetCodeElementFromUserData(Node node)
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
                if (!lastItemIsSeparator)
                {
                    contextMenu.Items.Add(new Separator());
                    lastItemIsSeparator = true;
                }

                continue;
            }

            if (!cmd.IsVisible || !cmd.CanHandle(element))
            {
                continue;
            }

            var menuItem = new MenuItem { Header = cmd.Label };

            // Add icon if provided
            if (cmd.Icon != null)
            {
                var iconImage = new Image
                {
                    Width = 16,
                    Height = 16,
                    Source = cmd.Icon
                };
                menuItem.Icon = iconImage;
            }

            menuItem.Click += (_, _) => cmd.Invoke(element);
            contextMenu.Items.Add(menuItem);
            lastItemIsSeparator = false;
        }
    }
}