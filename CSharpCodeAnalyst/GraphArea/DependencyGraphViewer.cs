using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using CodeParser.Extensions;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.GraphArea.RenderOptions;
using CSharpCodeAnalyst.Help;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;
using Color = Microsoft.Msagl.Drawing.Color;

namespace CSharpCodeAnalyst.GraphArea;

/// <summary>
///     Note:
///     Between nodes we can have multiple dependencies if the dependency type is different.
///     Dependencies of the same type (i.e a method Calls another multiple times) are handled
///     in the parser. In this case the dependency holds all source references.
/// </summary>
internal class DependencyGraphViewer : IDependencyGraphViewer, IDependencyGraphBinding, INotifyPropertyChanged
{
    private readonly List<IContextCommand> _contextCommands = [];
    private readonly MsaglBuilder _msaglBuilder;
    private readonly IPublisher _publisher;
    private readonly LinkedList<CodeGraph> _undoStack = new();

    private readonly int _undoStackSize = 10;

    /// <summary>
    ///     Held to read the help
    /// </summary>
    private IViewerObject? _clickedObject;

    private CodeGraph _clonedCodeGraph = new();


    private IQuickInfoFactory? _factory;

    private HighlightMode _highlightMode;
    private Color _lastHighlightedColor;

    private IViewerEdge? _lastHighlightedEdge;

    private GraphViewer? _msaglViewer;

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

        IntegrateNewFromOriginal(originalCodeElements);

        // Add dependencies we explicitly requested.
        foreach (var newDependency in newDependencies)
        {
            var sourceElement = _clonedCodeGraph.Nodes[newDependency.SourceId];
            sourceElement.Dependencies.Add(newDependency);
        }


        RefreshGraph();
    }

    public void AddContextCommand(IContextCommand command)
    {
        _contextCommands.Add(command);
    }

    public void Clear()
    {
        if (_msaglViewer is null)
        {
            return;
        }

        _clonedCodeGraph = new CodeGraph();
        _undoStack.Clear();
        RefreshGraph();
    }


    public void Reset()
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
        _highlightMode = valueMode;
        ClearEdgeColoring();
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


        item = new MenuItem { Header = "Delete all marked elements" };
        item.Click += (_, _) => DeleteAllMarkedElements();
        globalContextMenu.Items.Add(item);

        globalContextMenu.IsOpen = true;
    }

    public bool Undo()
    {
        if (_undoStack.Any() is false)
        {
            return false;
        }

        _clonedCodeGraph = _undoStack.First();
        _undoStack.RemoveFirst();
        RefreshGraph();
        return true;
    }


    public event PropertyChangedEventHandler? PropertyChanged;

    private void PushUndo()
    {
        if (_undoStack.Count >= _undoStackSize)
        {
            // Make space
            _undoStack.RemoveLast();
        }

        _undoStack.AddFirst(_clonedCodeGraph.Clone(null, null));
    }

    private void ClearEdgeColoring()
    {
        if (_msaglViewer is null)
        {
            return;
        }

        var edges = _msaglViewer.Entities.OfType<IViewerEdge>();
        foreach (var edge in edges)
        {
            edge.Edge.Attr.Color = Color.Black;
        }
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
            var graph = _msaglBuilder.CreateGraphFromCodeStructure(_clonedCodeGraph, _showFlatGraph);

            _renderOption.Apply(graph);
            _msaglViewer.Graph = graph;
        }
    }

    private void HighlightEdge(IViewerEdge? newEdge)
    {
        // Reset last highlighted edge
        if (_lastHighlightedEdge != null)
        {
            _lastHighlightedEdge.Edge.Attr.Color = _lastHighlightedColor;
            _msaglViewer?.Invalidate(_lastHighlightedEdge);
        }

        // Highlight new edge, if any
        if (newEdge != null)
        {
            _lastHighlightedColor = newEdge.Edge.Attr.Color;
            _lastHighlightedEdge = newEdge;
            newEdge.Edge.Attr.Color = Color.Red;
            _msaglViewer?.Invalidate(newEdge);
        }
    }

    private void ObjectUnderMouseCursorChanged(object? sender, ObjectUnderMouseCursorChangedEventArgs e)
    {
        if (_clickedObject is null)
        {
            // Update only if we don't hold the quick info.
            UpdateQuickInfoPanel(e.NewObject);
        }

        if (_highlightMode == HighlightMode.EdgeHovered)
        {
            HighlightEdge(e.NewObject as IViewerEdge);
        }

        if (_highlightMode == HighlightMode.OutgoingEdgesChildrenAndSelf)
        {
            HighlightOutgoingEdgesOfChildrenAndSelf(e.NewObject as IViewerNode);
        }
    }

    private void HighlightOutgoingEdgesOfChildrenAndSelf(IViewerNode? node)
    {
        if (_msaglViewer is null)
        {
            return;
        }

        var ids = new HashSet<string>();
        if (node != null)
        {
            var id = node.Node.Id;
            var vertex = _clonedCodeGraph.Nodes[id];
            ids = vertex.GetChildrenIncludingSelf();
        }

        var edges = _msaglViewer.Entities.OfType<IViewerEdge>();
        foreach (var edge in edges)
        {
            var sourceId = edge.Edge.Source;
            if (ids.Contains(sourceId))
            {
                edge.Edge.Attr.Color = Color.Red;
            }
            else
            {
                edge.Edge.Attr.Color = Color.Black;
            }
        }
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

            var addParentMenuItem = new MenuItem { Header = "Add parent" };
            addParentMenuItem.Click += (_, _) => AddParentRequest(node);

            var findInTreeMenuItem = new MenuItem { Header = "Find in Tree" };
            findInTreeMenuItem.Click += (_, _) => FindInTree(node);

            var deleteMenuItem = new MenuItem { Header = "Delete Node" };
            deleteMenuItem.Click += (_, _) => DeleteNode(node);

            contextMenu.Items.Add(deleteMenuItem);
            contextMenu.Items.Add(findInTreeMenuItem);
            contextMenu.Items.Add(addParentMenuItem);
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

    private void DeleteAllMarkedElements()
    {
        if (_msaglViewer is null)
        {
            return;
        }

        var ids = _msaglViewer.Entities.Where(e => e.MarkedForDragging).OfType<IViewerNode>().Select(n => n.Node.Id);
        foreach (var id in ids)
        {
            _clonedCodeGraph.RemoveCodeElement(id);
        }

        RefreshGraph();
    }

    private void AddMissingDependencies()
    {
        _publisher.Publish(new AddMissingDependenciesRequest());
    }

    private void AddParentRequest(Node node)
    {
        _publisher.Publish(new AddParentContainerRequest(node.Id));
    }


    private void FindInTree(Node node)
    {
        _publisher.Publish(new LocateInTreeRequest(node.Id));
    }
}