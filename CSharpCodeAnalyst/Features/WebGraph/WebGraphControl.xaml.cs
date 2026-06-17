using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
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
///     The data source is a mirror of the Code Explorer: it listens to the same
///     <see cref="IGraphViewer.GraphChanged" /> and serializes <see cref="IGraphViewer.GetGraph" />.
///     User interactions in the web view are translated back into existing MessageBus
///     messages (e.g. a node click fills the Info panel), so no new UI is needed.
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

    private IGraphViewer? _viewer;
    private IPublisher? _publisher;

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
    ///     Wires the web view to the same graph viewer the Code Explorer uses, so it
    ///     mirrors its content. Called once during application start-up.
    /// </summary>
    public void SetViewer(IGraphViewer viewer, IPublisher publisher)
    {
        _viewer = viewer;
        _publisher = publisher;
        _viewer.GraphChanged += OnViewerGraphChanged;

        // The hover-highlight mode is chosen in the ribbon; forward changes to JS,
        // which does the actual (local, per-hover) highlighting.
        _viewer.HighlightModeChanged += OnHighlightModeChanged;
    }

    private void OnHighlightModeChanged(HighlightMode mode)
    {
        Dispatcher.Invoke(() => PushHighlightMode(mode));
    }

    private void PushHighlightMode(HighlightMode mode)
    {
        _ = WebView.CoreWebView2?.ExecuteScriptAsync($"setHighlightMode('{mode}');");
    }

    private void OnViewerGraphChanged(CodeGraph.Graph.CodeGraph graph)
    {
        // GraphChanged is raised on the UI thread, but marshal defensively.
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
                RenderCurrentGraph();
                if (_viewer is not null)
                {
                    PushHighlightMode(_viewer.GetHighlightMode());
                }

                break;

            case "nodeClicked":
                PublishNodeInfo(message.Id);
                break;

            case "edgeClicked":
                PublishEdgeInfo(message.Source, message.Target);
                break;

            case "nodeDblClicked":
                ToggleCollapse(message.Id);
                break;

            case "backgroundClicked":
                // Clicking empty canvas clears the Info panel (same as the MSAGL view).
                _publisher?.Publish(new ClearQuickInfoRequest());
                break;
        }
    }

    /// <summary>
    ///     Double-click expands or collapses a container. We don't own the state: we ask
    ///     the shared viewer to toggle, it raises GraphChanged, and both views re-render.
    /// </summary>
    private void ToggleCollapse(string? id)
    {
        if (id is null || _viewer is null)
        {
            return;
        }

        var element = _viewer.GetGraph().TryGetCodeElement(id);
        if (element is null || element.Children.Count == 0)
        {
            // Leaf nodes have nothing to expand or collapse.
            return;
        }

        if (_viewer.IsCollapsed(id))
        {
            _viewer.Expand(id);
        }
        else
        {
            _viewer.Collapse(id);
        }
    }

    /// <summary>
    ///     Translates a web-side node click into the existing Info panel update, so the
    ///     web view reuses the same panel as the MSAGL view without any new UI.
    /// </summary>
    private void PublishNodeInfo(string? id)
    {
        if (id is null || _viewer is null || _publisher is null)
        {
            return;
        }

        var graph = _viewer.GetGraph();
        var element = graph.TryGetCodeElement(id);
        if (element is null)
        {
            return;
        }

        var factory = new QuickInfoFactory(graph);
        _publisher.Publish(new QuickInfoUpdateRequest([factory.CreateForCodeElement(element)]));
    }

    /// <summary>
    ///     A bundled edge stands for all relationships between the two nodes; clicking it
    ///     shows the full list in the Info panel.
    /// </summary>
    private void PublishEdgeInfo(string? sourceId, string? targetId)
    {
        if (sourceId is null || targetId is null || _viewer is null || _publisher is null)
        {
            return;
        }

        var graph = _viewer.GetGraph();
        var relationships = WebGraphBuilder.GetBundledRelationships(graph, _viewer.IsCollapsed, sourceId, targetId);
        if (relationships.Count == 0)
        {
            return;
        }

        var factory = new QuickInfoFactory(graph);
        _publisher.Publish(new QuickInfoUpdateRequest(factory.CreateForRelationships(relationships)));
    }

    private sealed class HostMessage
    {
        public string? Type { get; set; }
        public string? Id { get; set; }
        public string? Source { get; set; }
        public string? Target { get; set; }
    }

    private void RenderCurrentGraph()
    {
        if (!_isWebReady || _viewer is null)
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

        var json = WebGraphBuilder.BuildJson(_viewer.GetGraph(), _viewer.IsCollapsed);
        _ = core.ExecuteScriptAsync($"renderGraph({json});");
    }
}
