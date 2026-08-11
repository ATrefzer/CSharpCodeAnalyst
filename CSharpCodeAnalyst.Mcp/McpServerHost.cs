using System.Net;
using System.Reflection;
using System.Text.Json;
using CSharpCodeAnalyst.Mcp.Contracts;
using CSharpCodeAnalyst.Mcp.Tools;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CSharpCodeAnalyst.Mcp;

/// <summary>
///     Runs the MCP server inside the host application: an <see cref="HttpListener" /> endpoint that
///     speaks the Model Context Protocol over HTTP, so an assistant can query the currently loaded
///     code graph.
///     <para>
///         Deliberately not on ASP.NET. Kestrel would be the obvious host, but the package pulls a
///         framework reference that propagates into the executable's runtimeconfig - and the .NET host
///         refuses to start a process whose declared frameworks are not all installed. A missing
///         ASP.NET runtime would then not cost the MCP feature, it would cost the whole application.
///         HttpListener is part of the base runtime, so this host has nothing that can be missing.
///     </para>
///     <para>
///         Bound to loopback only, and deliberately so. The graph describes someone's source code in
///         full - assembly, namespace and member names, file paths, call structure. Binding to
///         anything else would publish that to the network.
///     </para>
/// </summary>
public sealed class McpServerHost : IAsyncDisposable
{
    public const string EndpointPath = "/mcp";

    private Task? _acceptLoop;
    private CancellationTokenSource? _shutdown;
    private HttpListener? _listener;
    private McpServerOptions? _options;

    public bool IsRunning => _listener is not null;

    public Uri? Endpoint { get; private set; }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Starts listening. Throws if the port is taken - the caller decides whether that is fatal or
    ///     merely means "no MCP this session", because only it knows whether the user asked for the
    ///     server explicitly.
    /// </summary>
    public Task StartAsync(ICodeGraphSnapshotSource snapshotSource, int port,
        CancellationToken cancellationToken = default)
    {
        if (_listener is not null)
        {
            throw new InvalidOperationException("The MCP server is already running.");
        }

        _options = CreateOptions(snapshotSource);

        var listener = new HttpListener();

        // The whole port rather than just the endpoint path: HttpListener matches prefixes, and how it
        // treats a request without the trailing slash is not worth depending on. The path is checked
        // below, where the answer for a wrong one can be a 404 instead of a connection reset.
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        _listener = listener;
        _shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Task.Run, and it is not decoration. This is started from the UI thread, so without it the
        // first await inside the loop would capture the dispatcher and post every continuation back to
        // it - accept, request parsing and response teardown would all run on the UI thread, and the
        // application would stutter for every request. Kestrel had its own threads and hid this.
        _acceptLoop = Task.Run(() => AcceptLoopAsync(listener, _shutdown.Token), CancellationToken.None);

        Endpoint = new Uri($"http://127.0.0.1:{port}{EndpointPath}");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        var listener = _listener;
        if (listener is null)
        {
            return;
        }

        _listener = null;
        Endpoint = null;

        if (_shutdown is not null)
        {
            await _shutdown.CancelAsync().ConfigureAwait(false);
        }

        // Closing is what unblocks GetContextAsync; the accept loop then ends on its own.
        listener.Close();

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The loop ends by having its listener closed underneath it. Whatever that throws is
                // the shutdown itself, not a failure worth surfacing.
            }
        }

        _shutdown?.Dispose();
        _shutdown = null;
        _acceptLoop = null;
        _options = null;
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (!listener.IsListening)
            {
                return;
            }

            // One slow request must not hold up the next one.
            _ = HandleSafelyAsync(context, cancellationToken);
        }
    }

    private async Task HandleSafelyAsync(HttpListenerContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await HandleAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            try
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            catch (Exception)
            {
                // The response may already be on its way out; nothing left to report with.
            }
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch (Exception)
            {
                // Client hung up first.
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        if (!string.Equals(request.Url?.AbsolutePath.TrimEnd('/'), EndpointPath,
                StringComparison.OrdinalIgnoreCase))
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            return;
        }

        if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            // Stateless: there is no stream of unsolicited messages for a client to hang a GET on.
            response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            return;
        }

        JsonRpcMessage? message;
        try
        {
            message = await JsonSerializer.DeserializeAsync<JsonRpcMessage>(
                    request.InputStream, McpJsonUtilities.DefaultOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // Malformed input is the client's mistake, not ours. A 500 would send it looking in the
            // wrong place - most often at a shell that mangled the quotes.
            response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        if (message is null)
        {
            response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        // The status has to be decided before anything is written, because writing to the body flushes
        // the headers and freezes it. Only a request produces an answer; a notification carries no id
        // and is acknowledged with 202 and an empty body.
        var expectsAnswer = message is JsonRpcRequest;
        if (expectsAnswer)
        {
            response.ContentType = "text/event-stream";
            response.StatusCode = (int)HttpStatusCode.OK;
        }
        else
        {
            response.StatusCode = (int)HttpStatusCode.Accepted;
        }

        await using var transport = new StreamableHttpServerTransport { Stateless = true };
        await using var server = McpServer.Create(transport, _options!);

        var running = server.RunAsync(cancellationToken);

        await transport.HandlePostRequestAsync(message, response.OutputStream, cancellationToken)
            .ConfigureAwait(false);

        await transport.DisposeAsync().ConfigureAwait(false);
        await running.ConfigureAwait(false);
    }

    /// <summary>
    ///     The tools, built by hand rather than through dependency injection. Without a service
    ///     provider there is nothing to resolve them from, and three constructor calls are a smaller
    ///     thing to own than a container.
    /// </summary>
    private static McpServerOptions CreateOptions(ICodeGraphSnapshotSource snapshotSource)
    {
        var tools = new McpServerPrimitiveCollection<McpServerTool>();

        object[] toolTypes =
        [
            new GraphInfoTools(snapshotSource),
            new ElementTools(snapshotSource),
            new RelationshipTools(snapshotSource)
        ];

        foreach (var target in toolTypes)
        {
            foreach (var method in target.GetType()
                         .GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<McpServerToolAttribute>() is null)
                {
                    continue;
                }

                tools.Add(McpServerTool.Create(method, target));
            }
        }

        return new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = ServerIdentity.Name,
                Version = ServerIdentity.GetVersion()
            },
            ServerInstructions = ServerIdentity.Instructions,

            // The collection hangs off the options directly, not off the capability. Setting it is
            // what advertises the tools capability - ToolsCapability itself only carries ListChanged.
            ToolCollection = tools
        };
    }
}
