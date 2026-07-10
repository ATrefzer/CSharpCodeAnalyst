using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CSharpCodeAnalyst.TreeMap.Drawing;
using CSharpCodeAnalyst.Contracts;
using CSharpCodeAnalyst.TreeMap.Interfaces;
using CSharpCodeAnalyst.TreeMap.Tools;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace CSharpCodeAnalyst.TreeMap;

/// <summary>
///     Note about the coloring:
///     Filtering is always done on a clone of the original tree. Filtering leads to recalculation of the weights (colors).
///     This happens when we change the filters in the tool window.
///     Changing the zoom level only sets a different entry point to display. It does not recalculate the weights.
///     Therefore, the zoom level does not affect the coloring even if the data with the most significant weights are not
///     visible.
/// </summary>
public abstract class HierarchicalDataViewBase : UserControl
{
    public static readonly DependencyProperty UserCommandsProperty = DependencyProperty.Register(
        nameof(UserCommands), typeof(HierarchicalDataCommands), typeof(HierarchicalDataViewBase),
        new PropertyMetadata(null));

    private readonly MenuItem _toolMenuItem = new()
        { Header = "Tools", Tag = null };

    /// <summary>
    ///     Original data, untouched
    /// </summary>
    private IHierarchicalData? _originalData;

    private IRenderer? _renderer;

    private ToolView? _toolView;
    private ToolViewModel? _toolViewModel;

    /// <summary>
    ///     Sub tree to display. This is the current zoom level shown.
    ///     The weights (colors) are not adjusted when we change the zoom level.
    ///     This may be a clone of the original data (when filtered) or
    ///     a reference to the original data (no filter)
    /// </summary>
    private IHierarchicalData? _zoomLevel;

    /// <summary>
    ///     Supplied by the data context: creates the placeholder shown when a filter removes every
    ///     node. The control only knows the interface, so it cannot build a concrete node itself.
    /// </summary>
    private Func<IHierarchicalData>? _createNoData;

    protected IBrushFactory? BrushFactory { get; set; }

    /// <summary>
    ///     Commands that apply to leaf nodes of a hierarchical data.
    /// </summary>
    public HierarchicalDataCommands? UserCommands
    {
        get => (HierarchicalDataCommands)GetValue(UserCommandsProperty);
        set => SetValue(UserCommandsProperty, value);
    }

    protected void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // The single TreeMapView instance is reused across all tree-map tabs (same DataTemplate),
        // so switching tabs just rebinds it. The tool window belongs to the data we were showing -
        // close it here so the newly shown tab can open its own.
        HideToolView();

        _originalData = null;
        BrushFactory = null;

        if (DataContext is not HierarchicalDataContext context)
        {
            // This is called once with the wrong context.
            return;
        }

        if (context.AreaSemantic == null || context.WeightSemantic == null)
        {
            return;
        }

        BrushFactory = context.BrushFactory;
        _originalData = context.Data;
        _createNoData = context.CreateNoData;

        InitializeTools(context.AreaSemantic, context.WeightSemantic);

        // Weights arrive raw - the view owns the normalization. DoFilter re-normalizes
        // whenever leaves are removed by a filter change.
        _originalData.NormalizeWeightMetrics();

        // Initially no filtering so skip removing nodes.
        ZoomLevelChanged(_originalData);
    }

    private void OnToolHighlightPatternChanged(object? sender, EventArgs args)
    {
        if (_zoomLevel is null)
        {
            return;
        }

        // Render again with new highlighting
        DoRender(_zoomLevel);
    }

    private void ChangeZoomLevelCommand(IHierarchicalData? item)
    {
        if (item == null)
        {
            return;
        }

        // Note: source tree is already filtered.
        ZoomLevelChanged(item);
    }

    protected abstract void ClosePopup();

    protected abstract IRenderer CreateRenderer();

    private IHierarchicalData DoFilter(IHierarchicalData data)
    {
        if (_toolViewModel is null || _toolViewModel.NoFilterJustHighlight)
        {
            // Highlighting the filter instead of removing the nodes.
            return data;
        }

        data.RemoveLeafNodes(leaf =>
            !_toolViewModel.IsAreaValid(leaf.AreaMetric) ||
            !_toolViewModel.IsWeightValid(leaf.WeightMetric));

        try
        {
            data.RemoveLeafNodesWithoutArea();
        }
        catch (Exception)
        {
            // The filter removed everything - show the producer-supplied placeholder.
            if (_createNoData != null)
            {
                data = _createNoData();
            }
        }

        // After we removed weights we have to normalize again.
        data.SumAreaMetrics(); // Only TreeMapView
        data.NormalizeWeightMetrics();

        return data;
    }

    private void OnToolFilterChanged(object? sender, EventArgs args)
    {
        if (_originalData is null)
        {
            return;
        }

        var oldZoomLevelPath = _zoomLevel?.GetPathToRoot();
        var sourceData = DoFilter(_originalData.Clone());

        var zoomTo = FindByPath(sourceData, oldZoomLevelPath);

        ZoomLevelChanged(zoomTo);
    }

    private IHierarchicalData FindByPath(IHierarchicalData tree, string? path)
    {
        if (path == null)
        {
            return tree;
        }

        // Re-locate the previously zoomed node in the freshly filtered clone by its path to
        // root. A node's path is unique (siblings never share a name) and stable across the
        // clone, so it identifies the same logical node without a separate Id field. Falls back
        // to the root when the node no longer exists (its subtree was filtered away).
        return tree.FirstOrDefault(node => node.GetPathToRoot() == path) ?? tree;
    }


    protected abstract DrawingCanvas GetCanvas();


    protected void HideToolView()
    {
        // When the control is no longer visible close the tool window.
        _toolView?.Close();
        _toolView = null;
        _zoomLevel = null;
    }


    private void InitializeTools(string areaSemantic, string weightSemantic)
    {
        var area = new HashSet<double>();
        var weight = new HashSet<double>();

        if (_originalData is null)
        {
            return;
        }

        // Distinct areas and weights. Each slider tick goes to the next value.
        // This allows smooth navigation even if there are large outliers.
        _originalData.TraverseTopDown(data =>
        {
            if (data.IsLeafNode)
            {
                area.Add(data.AreaMetric);
                weight.Add(data.WeightMetric);
            }
        });

        var areaList = area.OrderBy(x => x).ToList();
        var weightList = weight.OrderBy(x => x).ToList();

        _toolViewModel = new ToolViewModel(areaList, weightList)
        {
            AreaSemantic = areaSemantic,
            WeightSemantic = weightSemantic
        };

        _toolViewModel.FilterChanged += OnToolFilterChanged;
        _toolViewModel.HighlightPatternChanged += OnToolHighlightPatternChanged;
        _toolViewModel.Reset += OnToolReset;
    }

    private void OnToolReset(object? sender, EventArgs e)
    {
        if (_originalData == null)
        {
            return;
        }

        ZoomLevelChanged(_originalData);
    }


    protected abstract void InitPopup(IHierarchicalData hit);

    protected ContextMenu? GetContextMenu(object sender)
    {
        var fe = sender as FrameworkElement;
        return fe?.ContextMenu;
    }

    protected void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // Does not tell which one.
        if (_renderer == null || _zoomLevel == null)
        {
            e.Handled = true;
            return;
        }

        var canvas = GetCanvas();
        var pos = _renderer.Transform(Mouse.GetPosition(canvas));
        var hit = _renderer.Hit(_zoomLevel, pos);
        var menu = GetContextMenu(sender);
        if (hit != null && menu != null)
        {
            menu.Items.Clear();

            // Item for filter tool window
            _toolMenuItem.IsEnabled = _toolView is not { IsVisible: true };
            _toolMenuItem.Command = new DelegateCommand(ShowToolsCommand);
            menu.Items.Add(_toolMenuItem);

            UserCommands?.Fill(menu, hit);

            menu.Items.Add(new Separator());

            FillZoomLevels(menu, hit);
        }

        // Show context menu if at least one item is there.
        e.Handled = menu?.Items.Count == 0;
    }


    private void ShowToolsCommand()
    {
        // Filter
        _toolView = new ToolView
        {
            Owner = Application.Current.MainWindow,
            DataContext = _toolViewModel
        };
        _toolView.Show();
    }


    protected void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        ClosePopup();
    }


    protected void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_originalData == null || _renderer == null || _zoomLevel == null)
        {
            return;
        }

        ClosePopup();

        // Circle packing renderer uses transformations. So we have to translate the mouse position
        // into the coordinates of the circles.
        var pos = _renderer.Transform(e.GetPosition(GetCanvas()));
        var hit = _renderer.Hit(_zoomLevel, pos);
        if (hit != null)
        {
            InitPopup(hit);
        }
    }

    private void ZoomLevelChanged(IHierarchicalData? data)
    {
        if (data == null)
        {
            return;
        }

        DoRender(data);
    }

    private void DoRender(IHierarchicalData data)
    {
        _zoomLevel = data;
        _renderer = CreateRenderer();
        _renderer.LoadData(_zoomLevel);
        _renderer.Highlighting = new Highlighting(_toolViewModel!);
        GetCanvas().DataContext = _renderer;
    }

    private void AddZoomLevel(ContextMenu menu, IHierarchicalData data)
    {
        var header = data.GetPathToRoot();
        var menuItem = new MenuItem
        {
            Header = header,
            Command = new DelegateCommand(() => ChangeZoomLevelCommand(data))
        };
        menu.Items.Add(menuItem);
    }

    private void FillZoomLevels(ContextMenu menu, IHierarchicalData hit)
    {
        // From the current item (exclusive) up the the root 
        // add an context menu entry for each zoom level.

        var current = hit;
        while (current != null)
        {
            // Avoid unnecessary context menus
            if (current != _zoomLevel && !current.IsLeafNode)
            {
                AddZoomLevel(menu, current);
            }

            current = current.Parent;
        }
    }
}