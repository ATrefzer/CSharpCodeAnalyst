using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CodeParserTests.Helper;
using CSharpCodeAnalyst.Mcp;
using CSharpCodeAnalyst.Mcp.Contracts;

namespace CodeParserTests.UnitTests.Mcp;

/// <summary>
///     End to end tests for <see cref="McpServerHost" />: a real socket, a real HTTP
///     request, the real protocol.
///     <para>
///         The tool tests elsewhere call the tool methods directly and say nothing about whether a
///         client can reach them. This fixture covers the piece between - transport, routing and
///         serialization - which is the part this host owns rather than borrows from the SDK.
///     </para>
/// </summary>
[TestFixture]
public class McpServerHostTests
{
    [SetUp]
    public async Task SetUp()
    {
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("Sample.Core");
        var ns = graph.CreateNamespace("Services", assembly);
        graph.CreateClass("OrderService", ns);

        _port = FreePort();
        _host = new McpServerHost();
        await _host.StartAsync(FakeSnapshotSource.With(graph), _port);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _host.StopAsync();
    }

    private McpServerHost _host = null!;
    private int _port;

    /// <summary>
    ///     Asking the operating system for a free port rather than picking one: a fixed number turns
    ///     any other process on the machine - including a second run of this suite - into a failure
    ///     that looks like a defect.
    /// </summary>
    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task<(HttpStatusCode Status, string Body)> PostAsync(string body)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"http://127.0.0.1:{_port}/mcp", content);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Test]
    public void Start_ReportsTheEndpointAClientIsConfiguredWith()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_host.IsRunning, Is.True);
            Assert.That(_host.Endpoint?.ToString(),
                Is.EqualTo($"http://127.0.0.1:{_port}/mcp"));
        });
    }

    [Test]
    public async Task ToolsList_AnswersWithEveryRegisteredTool()
    {
        var (status, body) = await PostAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Contain("graph_info"));
            Assert.That(body, Does.Contain("search_elements"));
            Assert.That(body, Does.Contain("describe_element"));
            Assert.That(body, Does.Contain("find_inheritance"));
            Assert.That(body, Does.Contain("find_paths_between"));
            Assert.That(body, Does.Contain("find_incoming_calls"));
            Assert.That(body, Does.Contain("find_incoming_relationships"));
            Assert.That(body, Does.Contain("find_outgoing_relationships"));
        });
    }

    /// <summary>
    ///     The descriptions are the entire briefing a model gets, so they have to survive the trip
    ///     rather than only exist in the attribute.
    /// </summary>
    [Test]
    public async Task ToolsList_CarriesDescriptionsAndParameterSchemas()
    {
        var (_, body) = await PostAsync("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""");

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("This is the entry point for every other tool"));
            Assert.That(body, Does.Contain("Element id from search_elements."));
        });
    }

    [Test]
    public async Task ToolCall_AnswersFromTheLoadedSnapshot()
    {
        var (status, body) = await PostAsync(
            """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"graph_info","arguments":{}}}""");

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Contain("Code elements: 3"));
            Assert.That(body, Does.Contain("Sample.Core"));
        });
    }

    [Test]
    public async Task ToolCall_PassesArgumentsThrough()
    {
        var (_, body) = await PostAsync(
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"search_elements","arguments":{"query":"orderservice"}}}""");

        Assert.That(body, Does.Contain("OrderService"));
    }

    /// <summary>
    ///     A notification carries no id and expects no answer. The status has to say so, and it has to
    ///     be set before anything is written - once the body starts, the headers are gone.
    /// </summary>
    [Test]
    public async Task Notification_IsAcknowledgedWithoutAnAnswer()
    {
        var (status, body) = await PostAsync(
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        Assert.Multiple(() =>
        {
            Assert.That(status, Is.EqualTo(HttpStatusCode.Accepted));
            Assert.That(body, Is.Empty);
        });
    }

    /// <summary>
    ///     Malformed JSON is the caller's mistake. A 500 would send them looking at the server, which
    ///     is the wrong place - most often a shell mangled the quotes.
    /// </summary>
    [Test]
    public async Task MalformedJson_IsABadRequest()
    {
        var (status, _) = await PostAsync("{ this is not json");

        Assert.That(status, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task UnknownPath_IsNotFound()
    {
        using var client = new HttpClient();

        var response = await client.GetAsync($"http://127.0.0.1:{_port}/something-else");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    /// <summary>
    ///     Stateless: there is no stream of unsolicited messages, so there is nothing for a client to
    ///     hang a GET on. Answering 405 says that outright instead of leaving the request open.
    /// </summary>
    [Test]
    public async Task GetOnTheEndpoint_IsMethodNotAllowed()
    {
        using var client = new HttpClient();

        var response = await client.GetAsync($"http://127.0.0.1:{_port}/mcp");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
    }

    [Test]
    public void StartingTwice_IsRefused()
    {
        Assert.ThatAsync(() => _host.StartAsync(FakeSnapshotSource.Empty(), _port),
            Throws.InstanceOf<InvalidOperationException>());
    }

    /// <summary>
    ///     Stopping has to give the port back, or the button in the application can be switched off and
    ///     never on again.
    /// </summary>
    [Test]
    public async Task Stopping_ReleasesThePort()
    {
        await _host.StopAsync();

        var again = new McpServerHost();
        try
        {
            await again.StartAsync(FakeSnapshotSource.Empty(), _port);
            Assert.That(again.IsRunning, Is.True);
        }
        finally
        {
            await again.StopAsync();
        }
    }

    [Test]
    public async Task StoppingTwice_IsHarmless()
    {
        await _host.StopAsync();
        await _host.StopAsync();

        Assert.That(_host.IsRunning, Is.False);
    }

    /// <summary>
    ///     The server is started from the UI thread. Nothing about serving a request may go back there:
    ///     without the deliberate hop onto the thread pool, the first await inside the accept loop
    ///     captures the dispatcher, and from then on accepting connections, parsing request bodies and
    ///     closing responses all happen on the thread that draws the application.
    ///     <para>
    ///         Kestrel brought its own threads and hid this. On HttpListener the host has to arrange it,
    ///         so it is worth a test rather than a comment - the failure mode is a stuttering UI under
    ///         load, which nobody will trace back to here.
    ///     </para>
    /// </summary>
    [Test]
    public async Task ServingARequest_NeverReturnsToTheCallersThread()
    {
        var port = FreePort();
        using var caller = new SingleThreadContext();
        var source = new RecordingSource();
        var host = new McpServerHost();

        // Start under the fake dispatcher, the way the application starts it from the UI thread.
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(caller);
        try
        {
            await host.StartAsync(source, port);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
            using var content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"graph_info","arguments":{}}}""",
                Encoding.UTF8, "application/json");

            await client.PostAsync($"http://127.0.0.1:{port}/mcp", content);
        }
        finally
        {
            await host.StopAsync();
        }

        Assert.Multiple(() =>
        {
            Assert.That(caller.Posts, Is.Zero,
                "nothing may be scheduled back onto the thread that started the server");
            Assert.That(source.ThreadIdWhenQueried, Is.Not.EqualTo(caller.ThreadId),
                "the graph must not be read on the caller's thread either");
        });
    }

    /// <summary>
    ///     A stand-in for the WPF dispatcher: a single thread with a queue, and the context stays
    ///     current on it - so anything posted here keeps everything downstream on that one thread,
    ///     which is precisely what the UI thread does.
    /// </summary>
    private sealed class SingleThreadContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
        private readonly Thread _thread;

        private int _posts;

        public SingleThreadContext()
        {
            _thread = new Thread(() =>
            {
                SetSynchronizationContext(this);
                foreach (var (callback, state) in _queue.GetConsumingEnumerable())
                {
                    callback(state);
                }
            }) { IsBackground = true, Name = "FakeDispatcher" };

            _thread.Start();
        }

        public int Posts => Volatile.Read(ref _posts);

        public int ThreadId => _thread.ManagedThreadId;

        public void Dispose()
        {
            _queue.CompleteAdding();
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _posts);
            _queue.Add((d, state));
        }
    }

    /// <summary>Records where the graph was actually read from.</summary>
    private sealed class RecordingSource : ICodeGraphSnapshotSource
    {
        public int ThreadIdWhenQueried;

        public Task<GraphSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            ThreadIdWhenQueried = Environment.CurrentManagedThreadId;
            return Task.FromResult<GraphSnapshot?>(new GraphSnapshot(new TestCodeGraph()));
        }
    }
}
