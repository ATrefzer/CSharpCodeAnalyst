using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.GraphArea.Highlighting;
using CSharpCodeAnalyst.GraphArea.RenderOptions;
using CSharpCodeAnalyst.Help;
using CSharpCodeAnalyst.Resources;
using Microsoft.Msagl.Core.Routing;
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
public class GraphViewer : IGraphViewer, IGraphBinding, INotifyPropertyChanged
{
    private readonly Stopwatch _clickStopwatch = Stopwatch.StartNew();
    private readonly List<IRelationshipContextCommand> _edgeCommands = [];
    private readonly List<IGlobalContextCommand> _globalCommands = [];
    private readonly int _maxElementWarningLimit;
    private readonly MsaglBuilder _msaglBuilder;
    private readonly List<ICodeElementContextCommand> _nodeCommands = [];
    private readonly IPublisher _publisher;

    private IHighlighting _activeHighlighting = new EdgeHoveredHighlighting();

    ClickController? _clickController;

    /// <summary>
    ///     Held to read the help
    /// </summary>
    private IViewerObject? _clickedObject;

    private CodeGraph _clonedCodeGraph = new();
    private IQuickInfoFactory? _factory;
    private bool _flow;
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
    public GraphViewer(IPublisher publisher, int settings)
    {
        _publisher = publisher;
        _msaglBuilder = new MsaglBuilder();
        SetHighlightMode(HighlightMode.EdgeHovered);
        _maxElementWarningLimit = settings;
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
    }


    public void ShowFlatGraph(bool value)
    {
        _showFlatGraph = value;
        RefreshGraph();
    }

    public void ShowInformationFlow(bool value)
    {
        _flow = value;
        RefreshGraph();
    }

    /// <summary>
    ///     Adding an existing element or relationship is prevented.
    ///     Note from the originalCodeElement we don't add parent or children.
    ///     We just use this information to integrate the node into the existing canvas.
    /// </summary>
    public void AddToGraph(IEnumerable<CodeElement> originalCodeElements, IEnumerable<Relationship> newRelationships, bool addCollapsed)
    {
        if (!IsBoundToPanel())
        {
            return;
        }

        var original = originalCodeElements.ToList();
        AddToGraphInternal(original, newRelationships);

        if (addCollapsed)
        {
            foreach (var codeElement in original.Where(c => c.ElementType == CodeElementType.Assembly || c.ElementType == CodeElementType.Namespace))
            {
                _presentationState.SetCollapsedState(codeElement.Id, true);
            }
        }

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
                _activeHighlighting = new OutgoingEdgesOfChildrenAndSelfHighlighting();
                break;
            case HighlightMode.ShortestNonSelfCircuit:
                _activeHighlighting = new HighlightShortestNonSelfCircuit();
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

        if (!idsToRemove.Any())
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

    public bool IsFlagged(string id)
    {
        return _presentationState.IsFlagged(id);
    }

    public void ToggleFlag(string id)
    {
        var currentState = _presentationState.IsFlagged(id);
        var newState = !currentState;
        _presentationState.SetFlaggedState(id, newState);
        RefreshFlagsWithoutLayout([id], newState);
    }

    public void ClearAllFlags()
    {
        var ids = _presentationState.NodeIdToFlagged.Keys.ToList();
        _presentationState.ClearAllFlags();
        RefreshFlagsWithoutLayout(ids, false);
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

    private void RefreshFlagsWithoutLayout(List<string> ids, bool isFlagged)
    {
        foreach (var id in ids)
        {
            var node = _msaglViewer?.Graph.FindNode(id);
            if (node is null)
            {
                // Unexpected.
                RefreshGraph();
                break;
            }

            if (isFlagged)
            {
                node.Attr.Color = Constants.FlagColor;
                node.Attr.LineWidth = Constants.FlagLineWidth;
            }
            else
            {
                node.Attr.Color = Constants.DefaultLineColor;
                node.Attr.LineWidth = Constants.DefaultLineWidth;
            }
        }
    }

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

    private HashSet<string> GetSelectedElementIds()
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
                var graph = _msaglBuilder.CreateGraph(_clonedCodeGraph, _presentationState,
                    _showFlatGraph, _flow);

                if (askUserToShowLargeGraphs)
                {
                    var elements = graph.Nodes.Count() + graph.Edges.Count() + graph.SubgraphMap.Count;
                    if (!ShouldProceedWithLargeGraph(elements))
                    {
                        _msaglViewer.Graph = new Graph();
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

    private void AddContextMenuEntries(List<Relationship> relationships, ContextMenu contextMenu)
    {
        if (relationships.Count == 0)
        {
            return;
        }

        foreach (var cmd in _edgeCommands)
        {
            if (cmd.CanHandle(relationships))
            {
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