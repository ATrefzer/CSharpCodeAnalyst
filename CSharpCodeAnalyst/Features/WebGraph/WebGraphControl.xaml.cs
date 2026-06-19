using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using CSharpCodeAnalyst.Features.Graph;
using CSharpCodeAnalyst.Features.Graph.RenderOptions;
using CSharpCodeAnalyst.Features.Help;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messages;
using Microsoft.Web.WebView2.Core;

namespace CSharpCodeAnalyst.Features.WebGraph;

/// <summary>
///     Hosts a WebView2 that renders the code graph with Cytoscape.js.
///     It is a render adapter over the shared <see cref="GraphViewState" />: it observes
///     <see cref="GraphViewState.Changed" />, serializes the model's graph, and drives the
///     model back (expand/collapse, …). User interactions are translated into existing
///     MessageBus messages (e.g. a node click fills the Info panel), so no new UI is needed.
///     Assets are served strictly offline from the output directory via a virtual host.
/// </summary>
public partial class WebGraphControl : UserControl
{
    private static readonly JsonSerializerOptions MessageJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Virtual host. The page is reachable under https://csharp-code-analyst.local/index.html
    private const string VirtualHost = "csharp-code-analyst.local";

    private bool _initialized;

    // True once the JS side reported {type:"ready"} and can accept renderGraph() calls.
    private bool _isWebReady;

    // A graph change arrived while the tab was hidden; render when it becomes visible.
    private bool _pendingRender;

    // Coalesces bursts of GraphChanged events into a single (expensive) re-layout.
    private readonly DispatcherTimer _renderDebounce;

    private GraphViewState? _state;
    private IPublisher? _publisher;

    // Relationships behind each drawn edge, keyed by edge id, captured at render time
    // (the web equivalent of MSAGL's edge.UserData). A click/right-click on an edge looks
    // it up here instead of reconstructing it from the topology.
    private IReadOnlyDictionary<string, WebEdgeInfo> _edgeInfos = new Dictionary<string, WebEdgeInfo>();

    public WebGraphControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        // Cytoscape needs a correctly sized container. While the tab is hidden the
        // WebView has no size, so we defer rendering and re-fit once it is shown again.
        IsVisibleChanged += OnIsVisibleChanged;

        _renderDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _renderDebounce.Tick += (_, _) =>
        {
            _renderDebounce.Stop();
            RenderCurrentGraph();
        };
    }

    /// <summary>
    ///     Wires the web view to the shared model both views use. Called once at start-up.
    /// </summary>
    public void SetViewer(GraphViewState state, IPublisher publisher, ISubscriber subscriber)
    {
        _state = state;
        _publisher = publisher;
        _state.Changed += OnStateChanged;

        // The hover-highlight mode is chosen in the ribbon; forward changes to JS,
        // which does the actual (local, per-hover) highlighting.
        _state.HighlightModeChanged += OnHighlightModeChanged;

        // Flags / search highlights restyle existing elements without a re-layout.
        _state.DecorationsChanged += OnDecorationsChanged;

        // The ribbon's Layout split button drives render-only operations. The model is
        // unchanged, so these come over the bus rather than through GraphViewState.Changed.
        subscriber.Subscribe<RelayoutGraphRequest>(_ => Dispatcher.Invoke(Relayout));
        subscriber.Subscribe<RefitGraphRequest>(_ => Dispatcher.Invoke(Refit));
    }

    private void OnDecorationsChanged()
    {
        Dispatcher.Invoke(PushDecorations);
    }

    /// <summary>
    ///     Restyles the existing elements for flags / search highlights — no re-layout. The
    ///     elements survive from the last render, so this works even while the tab is hidden.
    /// </summary>
    private void PushDecorations()
    {
        if (!_isWebReady || _state is null)
        {
            return;
        }

        var ps = _state.PresentationState;
        var flaggedNodes = _state.CodeGraph.Nodes.Keys.Where(ps.IsFlagged).ToList();
        var searchNodes = _state.CodeGraph.Nodes.Keys.Where(ps.IsSearchHighlighted).ToList();
        var flaggedEdges = _edgeInfos
            .Where(kv => ps.IsFlagged((kv.Value.SourceId, kv.Value.TargetId)))
            .Select(kv => kv.Key)
            .ToList();

        var payload = JsonSerializer.Serialize(new { flaggedNodes, searchNodes, flaggedEdges });
        _ = WebView.CoreWebView2?.ExecuteScriptAsync($"setDecorations({payload});");
    }

    /// <summary>
    ///     Re-runs the layout on the current elements (full reposition). When hidden we defer
    ///     to a full re-render on becoming visible, since fcose needs a sized container.
    /// </summary>
    private void Relayout()
    {
        if (!_isWebReady)
        {
            return;
        }

        if (IsVisible)
        {
            _ = WebView.CoreWebView2?.ExecuteScriptAsync("relayoutGraph();");
        }
        else
        {
            _pendingRender = true;
        }
    }

    /// <summary>Recomputes size and fits the view without re-running the layout.</summary>
    private void Refit()
    {
        if (_isWebReady && IsVisible)
        {
            _ = WebView.CoreWebView2?.ExecuteScriptAsync("refitGraph();");
        }
    }

    private void OnHighlightModeChanged(HighlightMode mode)
    {
        Dispatcher.Invoke(() => PushHighlightMode(mode));
    }

    private void PushHighlightMode(HighlightMode mode)
    {
        _ = WebView.CoreWebView2?.ExecuteScriptAsync($"setHighlightMode('{mode}');");
    }

    private void OnStateChanged()
    {
        // Changed is raised on the UI thread, but marshal defensively.
        Dispatcher.Invoke(() =>
        {
            if (IsVisible)
            {
                // Restart the debounce: many events in a row collapse into one render.
                _renderDebounce.Stop();
                _renderDebounce.Start();
            }
            else
            {
                // Don't lay out into a zero-size container; do it when shown.
                _pendingRender = true;
            }
        });
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible || !_isWebReady)
        {
            return;
        }

        if (_pendingRender)
        {
            // A change happened while hidden -> render it now with a correct size.
            RenderCurrentGraph();
        }
        else
        {
            // Nothing changed, but the viewport may be stale -> just resize and re-fit.
            _ = WebView.CoreWebView2?.ExecuteScriptAsync("refitGraph();");
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded can fire more than once (tab switches). Initialize the WebView only once.
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        try
        {
            await InitializeWebViewAsync();
        }
        catch (Exception ex)
        {
            // Keep a missing WebView2 runtime from taking the whole app down.
            Debug.WriteLine($"[WebGraph] WebView2 init failed: {ex}");
            MessageBox.Show($"WebView2 initialization failed:\n{ex.Message}", "Web Graph View",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task InitializeWebViewAsync()
    {
        // Use an explicit, writable user-data folder so the app also works when
        // installed under a read-only location (e.g. Program Files).
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CSharpCodeAnalyst", "WebView2");
        Directory.CreateDirectory(userDataFolder);

        // Transparent default background: until the page paints (during the cold start) the
        // WPF LoadingOverlay behind the WebView shows through instead of a white flash.
        WebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

        var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await WebView.EnsureCoreWebView2Async(environment);

        var core = WebView.CoreWebView2;

        // Development: never serve cached HTML/JS so edited assets always take effect.
        await core.CallDevToolsProtocolMethodAsync(
            "Network.setCacheDisabled", "{\"cacheDisabled\":true}");

        // Serve the local Web folder (copied next to the exe) under the virtual host.
        var webRoot = Path.Combine(AppContext.BaseDirectory, "Features", "WebGraph", "Web");
        core.SetVirtualHostNameToFolderMapping(
            VirtualHost, webRoot, CoreWebView2HostResourceAccessKind.Allow);

        core.WebMessageReceived += OnWebMessageReceived;

        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = true; // helpful during development

        WebView.Source = new Uri($"https://{VirtualHost}/index.html");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        HostMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<HostMessage>(e.WebMessageAsJson, MessageJsonOptions);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[WebGraph] could not parse message '{e.WebMessageAsJson}': {ex.Message}");
            return;
        }

        if (message?.Type is null)
        {
            return;
        }

        switch (message.Type)
        {
            case "ready":
                _isWebReady = true;

                // The page has painted; drop the cold-start placeholder for good.
                LoadingOverlay.Visibility = Visibility.Collapsed;

                RenderCurrentGraph();
                if (_state is not null)
                {
                    PushHighlightMode(_state.HighlightMode);
                }

                break;

            case "nodeClicked":
                PublishNodeInfo(message.Id);
                break;

            case "edgeClicked":
                PublishEdgeInfo(message.Id);
                break;

            case "nodeDblClicked":
                ToggleCollapse(message.Id);
                break;

            case "backgroundClicked":
                // Clicking empty canvas clears the Info panel (same as the MSAGL view).
                _publisher?.Publish(new ClearQuickInfoRequest());
                break;

            case "selectionChanged":
                UpdateSelection(message.Ids);
                break;

            case "contextMenu":
                ShowContextMenu(message);
                break;
        }
    }

    /// <summary>
    ///     Double-click expands or collapses a container. We don't own the state: we ask
    ///     the shared model to toggle, it raises Changed, and both views re-render.
    /// </summary>
    private void ToggleCollapse(string? id)
    {
        if (id is null || _state is null)
        {
            return;
        }

        var element = _state.CodeGraph.TryGetCodeElement(id);
        if (element is null || element.Children.Count == 0)
        {
            // Leaf nodes have nothing to expand or collapse.
            return;
        }

        if (_state.IsCollapsed(id))
        {
            _state.Expand(id);
        }
        else
        {
            _state.Collapse(id);
        }
    }

    /// <summary>
    ///     Translates a web-side node click into the existing Info panel update, so the
    ///     web view reuses the same panel as the MSAGL view without any new UI.
    /// </summary>
    private void PublishNodeInfo(string? id)
    {
        if (id is null || _state is null || _publisher is null)
        {
            return;
        }

        var graph = _state.CodeGraph;
        var element = graph.TryGetCodeElement(id);
        if (element is null)
        {
            return;
        }

        var factory = new QuickInfoFactory(graph);
        _publisher.Publish(new QuickInfoUpdateRequest([factory.CreateForCodeElement(element)]));
    }

    /// <summary>
    ///     A drawn edge stands for one or more relationships (a bundle when several);
    ///     clicking it shows the full list in the Info panel. The relationships were
    ///     captured at render time (<see cref="_edgeInfos" />), so we just look them up.
    /// </summary>
    private void PublishEdgeInfo(string? edgeId)
    {
        if (edgeId is null || _state is null || _publisher is null)
        {
            return;
        }

        if (!_edgeInfos.TryGetValue(edgeId, out var edge) || edge.Relationships.Count == 0)
        {
            return;
        }

        var factory = new QuickInfoFactory(_state.CodeGraph);
        _publisher.Publish(new QuickInfoUpdateRequest(factory.CreateForRelationships(edge.Relationships)));
    }

    private void UpdateSelection(List<string>? ids)
    {
        // The web selection is now canonical in the shared model; feed it there so the
        // toolbar / global commands (in the view model) act on it.
        _state?.SetSelection(ids ?? []);
    }

    /// <summary>
    ///     Builds the matching WPF context menu (reusing the existing command objects)
    ///     and opens it at the cursor. The commands act on the shared graph, so any change
    ///     flows back via GraphChanged and re-renders both views.
    /// </summary>
    private void ShowContextMenu(HostMessage message)
    {
        if (_state is null)
        {
            return;
        }

        var graph = _state.CodeGraph;

        var menu = message.Kind switch
        {
            "node" => BuildNodeMenu(graph, message.Id),
            "edge" => BuildEdgeMenu(message.Id),
            "background" => WebContextMenuFactory.BuildForGlobal(
                _state.GlobalCommands, GetSelectedElements(graph)),
            _ => null
        };

        if (menu is null || menu.Items.Count == 0)
        {
            return;
        }

        menu.PlacementTarget = WebView;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private ContextMenu? BuildNodeMenu(CodeGraph.Graph.CodeGraph graph, string? id)
    {
        var element = id is null ? null : graph.TryGetCodeElement(id);
        return element is null
            ? null
            : WebContextMenuFactory.BuildForNode(_state!.NodeCommands, element);
    }

    private ContextMenu? BuildEdgeMenu(string? edgeId)
    {
        if (edgeId is null || !_edgeInfos.TryGetValue(edgeId, out var edge) || edge.Relationships.Count == 0)
        {
            return null;
        }

        // The drawn (rerouted) endpoints are what the relationship commands expect, mirroring
        // MSAGL's edge.Source / edge.Target.
        return WebContextMenuFactory.BuildForEdge(_state!.EdgeCommands, edge.SourceId, edge.TargetId, edge.Relationships);
    }

    private List<CodeGraph.Graph.CodeElement> GetSelectedElements(CodeGraph.Graph.CodeGraph graph)
    {
        return _state!.SelectedIds
            .Select(graph.TryGetCodeElement)
            .OfType<CodeGraph.Graph.CodeElement>()
            .ToList();
    }

    private sealed class HostMessage
    {
        public string? Type { get; set; }
        public string? Kind { get; set; }

        // Node id, or — for edge messages — the edge id (source|target or source|type|target).
        public string? Id { get; set; }
        public List<string>? Ids { get; set; }
    }

    private void RenderCurrentGraph()
    {
        if (!_isWebReady || _state is null)
        {
            return;
        }

        var core = WebView.CoreWebView2;
        if (core is null)
        {
            return;
        }

        _renderDebounce.Stop();
        _pendingRender = false;

        // A re-render rebuilds all elements, so any selection in the web view is gone.
        _state.SetSelection([]);

        var data = WebGraphBuilder.Build(_state.CodeGraph, _state.IsCollapsed, _state.ShowFlat, _state.ShowInformationFlow, _state.HideFilter, _state.PresentationState);
        _edgeInfos = data.Edges;
        _ = core.ExecuteScriptAsync($"renderGraph({data.Json});");
    }
}
