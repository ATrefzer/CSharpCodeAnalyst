using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace CSharpCodeAnalyst.Features.WebGraph;

/// <summary>
///     Phase 0 spike: hosts a WebView2 that renders the graph with Cytoscape.js.
///     Assets are served strictly offline from the output directory via a virtual host.
///     The C# &lt;-&gt; JS bridge is wired but only logs for now.
/// </summary>
public partial class WebGraphControl : UserControl
{
    // Virtual host. The page is reachable under https://csharp-code-analyst.local/index.html
    private const string VirtualHost = "csharp-code-analyst.local";

    private bool _initialized;

    public WebGraphControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
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
            // Keep the spike from taking the whole app down if WebView2 runtime is missing.
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

        // Serve the local Web folder (copied next to the exe) under the virtual host.
        var webRoot = Path.Combine(AppContext.BaseDirectory, "Features", "WebGraph", "Web");
        core.SetVirtualHostNameToFolderMapping(
            VirtualHost, webRoot, CoreWebView2HostResourceAccessKind.Allow);

        core.WebMessageReceived += OnWebMessageReceived;

        // Lock the spike down a bit: no devtools menu, no default context menu.
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = true; // helpful during development

        WebView.Source = new Uri($"https://{VirtualHost}/index.html");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // Phase 0: just observe that the JS -> C# bridge works.
        // Phase 2 translates these into MessageBus messages (QuickInfoUpdateRequest, ...).
        var json = e.WebMessageAsJson;
        Debug.WriteLine($"[WebGraph] message from JS: {json}");
    }
}
