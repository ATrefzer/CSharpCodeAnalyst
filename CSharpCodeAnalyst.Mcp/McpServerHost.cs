using System.Reflection;
using CSharpCodeAnalyst.Mcp.Contracts;
using CSharpCodeAnalyst.Mcp.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace CSharpCodeAnalyst.Mcp;

/// <summary>
///     Runs the MCP server inside the host application: a Kestrel endpoint that speaks the Model
///     Context Protocol over HTTP, so an assistant can query the currently loaded code graph.
///     <para>
///         Bound to loopback only, and deliberately so. The graph describes someone's source code in
///         full - assembly, namespace and member names, file paths, call structure. Binding to
///         anything else would publish that to the network.
///     </para>
/// </summary>
public sealed class McpServerHost : IAsyncDisposable
{
    /// <summary>
    ///     The path the endpoint is mapped to. Part of the URL a client is configured with, so it is
    ///     named here rather than spelled out in the documentation twice.
    /// </summary>
    public const string EndpointPath = "/mcp";

    private WebApplication? _app;

    public bool IsRunning => _app is not null;

    /// <summary>
    ///     The URL to configure a client with, once started. Null while stopped.
    /// </summary>
    public Uri? Endpoint { get; private set; }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    /// <summary>
    ///     Starts listening. Throws if the port is taken - the caller decides whether that is fatal or
    ///     merely means "no MCP this session", because only it knows whether the user asked for the
    ///     server explicitly.
    /// </summary>
    public async Task StartAsync(ICodeGraphSnapshotSource snapshotSource, int port,
        CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            throw new InvalidOperationException("The MCP server is already running.");
        }

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            // The working directory of a desktop application is whatever the user last browsed to.
            // Pinning the content root keeps the web host from resolving configuration relative to it.
            ContentRootPath = AppContext.BaseDirectory,
            ApplicationName = typeof(McpServerHost).Assembly.GetName().Name
        });

        // Kestrel would otherwise log to a console this process does not have. Debug output keeps
        // startup failures visible while developing without adding a dependency on the host's logging.
        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();

        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(port));

        builder.Services.AddSingleton(snapshotSource);
        builder.Services
            .AddMcpServer(options =>
            {
                // Not the product name: this is one of the few strings a client shows a model, and the
                // product is named after a language it is not limited to. What it serves is a code
                // graph, whichever language that graph was built from.
                options.ServerInfo = new Implementation
                {
                    Name = "code-graph",
                    Version = GetVersion()
                };
                // The application is named after C#, and so is the name most clients are configured
                // with - but it imports C++, Dart, Python and Java as well, and the graph is the same
                // model for all of them. Naming the languages here, and saying outright that the name
                // does not carry the answer, is what keeps a caller from ruling the server out before
                // asking it anything.
                options.ServerInstructions =
                    "Answers questions about the code dependency graph currently loaded in CSharp Code " +
                    "Analyst: who calls what, what depends on what, how two elements are connected, and " +
                    "what a change would hit - dependencies, call graph, blast radius, architecture, " +
                    "layering. The loaded code base can be C#, C++, Dart, Python or Java; neither the " +
                    "name of this server nor the name of the application says which, so do not conclude " +
                    "from either that a question is out of scope. Call graph_info first: it reports the " +
                    "languages actually loaded, the size of the graph and how current it is. Element ids " +
                    "are opaque and only valid for the running server - always start with " +
                    "search_elements to obtain one.";
            })
            // Stateless: every request stands on its own. The tools are read only and answer from a
            // snapshot, so there is no per-session state worth keeping - and none to lose when a client
            // reconnects.
            .WithHttpTransport(transport => transport.Stateless = true)
            .WithTools<GraphInfoTools>()
            .WithTools<ElementTools>()
            .WithTools<RelationshipTools>();

        var app = builder.Build();
        app.MapMcp(EndpointPath);

        await app.StartAsync(cancellationToken);

        _app = app;
        Endpoint = new Uri($"http://127.0.0.1:{port}{EndpointPath}");
    }

    public async Task StopAsync()
    {
        var app = _app;
        if (app is null)
        {
            return;
        }

        _app = null;
        Endpoint = null;

        await app.StopAsync();
        await app.DisposeAsync();
    }

    private static string GetVersion()
    {
        var assembly = typeof(McpServerHost).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // A deterministic build appends "+<commit sha>" to the informational version. Useful in a
        // crash report, noise in a protocol field a client displays.
        var plus = informational?.IndexOf('+') ?? -1;
        if (plus > 0)
        {
            return informational![..plus];
        }

        return informational
               ?? assembly.GetName().Version?.ToString()
               ?? "0.0.0";
    }
}
