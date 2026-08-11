using System.Windows.Threading;
using CodeParserTests.Helper;
using CSharpCodeAnalyst.Features.Mcp;
using CSharpCodeAnalyst.Mcp.Contracts;

namespace CodeParserTests.UnitTests.Mcp;

/// <summary>
///     Tests for <see cref="CodeGraphSnapshotProvider" />: the bridge between the live, UI-thread-only
///     graph and the MCP tools, which read from a thread-pool thread and must never see a half-changed
///     structure.
///     <para>
///         A real <see cref="Dispatcher" /> is used throughout rather than a fake - the class dispatches
///         onto it by construction, and the one bug this fixture exists to pin (see
///         <see cref="ALostUpdateBetweenSetGraphAndAnInFlightCapture_MustNotResurrectTheOldSnapshot" />)
///         only exists because of how a real <see cref="Dispatcher" /> orders and completes queued
///         operations. A fake would not reproduce it.
///     </para>
/// </summary>
[TestFixture]
public class CodeGraphSnapshotProviderTests
{
    [SetUp]
    public void SetUp()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();

        _uiThread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        }) { IsBackground = true, Name = "FakeUiThread" };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
        ready.Wait();

        _dispatcher = dispatcher!;
        _provider = new CodeGraphSnapshotProvider(_dispatcher);
    }

    [TearDown]
    public void TearDown()
    {
        _dispatcher.InvokeShutdown();
    }

    private Dispatcher _dispatcher = null!;
    private CodeGraphSnapshotProvider _provider = null!;
    private Thread _uiThread = null!;

    private static bool HoldsAssembly(GraphSnapshot? snapshot, string assemblyName)
    {
        return snapshot is not null && snapshot.Graph.Nodes.Values.Any(n => n.Name == assemblyName);
    }

    [Test]
    public async Task NoGraphLoaded_AnswersNull()
    {
        var snapshot = await _provider.GetSnapshotAsync();

        Assert.That(snapshot, Is.Null);
    }

    [Test]
    public async Task AfterSetGraph_TheSnapshotReflectsIt()
    {
        var graph = new TestCodeGraph();
        graph.CreateAssembly("Sample");

        await _dispatcher.InvokeAsync(() => _provider.SetGraph(graph));
        var snapshot = await _provider.GetSnapshotAsync();

        Assert.That(HoldsAssembly(snapshot, "Sample"), Is.True);
    }

    /// <summary>
    ///     The snapshot is a clone, not the live graph, or a concurrent read would race the UI thread
    ///     mutating it in place during a refactoring simulation.
    /// </summary>
    [Test]
    public async Task TheSnapshot_IsAClone_NotTheLiveGraph()
    {
        var graph = new TestCodeGraph();
        graph.CreateAssembly("Sample");

        await _dispatcher.InvokeAsync(() => _provider.SetGraph(graph));
        var snapshot = await _provider.GetSnapshotAsync();

        Assert.That(snapshot!.Graph, Is.Not.SameAs(graph));
    }

    /// <summary>
    ///     "Nothing is left to answer a question with anyway" (see the doc comment on
    ///     <see cref="CodeGraphSnapshotProvider.Release" />) describes why the memory can safely be
    ///     given back when the server stops - not what <see cref="CodeGraphSnapshotProvider.GetSnapshotAsync" />
    ///     does afterwards. <c>Release</c> only drops the cached clone; the live graph it was cloned
    ///     from is untouched, so a query that still comes in (or one after the server is started again
    ///     without a fresh import) correctly recaptures it rather than answering null.
    /// </summary>
    [Test]
    public async Task Release_OnlyDropsTheCache_TheNextQueryStillAnswersFromTheLiveGraph()
    {
        var graph = new TestCodeGraph();
        graph.CreateAssembly("Sample");
        await _dispatcher.InvokeAsync(() => _provider.SetGraph(graph));
        await _provider.GetSnapshotAsync();

        _provider.Release();
        var snapshot = await _provider.GetSnapshotAsync();

        Assert.That(HoldsAssembly(snapshot, "Sample"), Is.True);
    }

    [Test]
    public async Task Release_WithNothingEverLoaded_IsHarmless()
    {
        _provider.Release();

        var snapshot = await _provider.GetSnapshotAsync();

        Assert.That(snapshot, Is.Null);
    }

    /// <summary>
    ///     Regression test for a lost-update race: capturing clones the graph on the UI thread and
    ///     takes real wall-clock time for anything but a trivial graph, so a <see cref="CodeGraphSnapshotProvider.SetGraph" />
    ///     can land while a capture that started against the *previous* graph is still in flight.
    ///     <para>
    ///         Both writers - the in-flight capture committing its result, and the new
    ///         <c>SetGraph</c> marking the cache stale - take the same lock, so one of them commits
    ///         last and wins. If the capture's commit wins, it stamps <c>_isStale = false</c> over a
    ///         snapshot of the graph as it was *before* the swap, silently resurrecting stale data
    ///         that nothing will refresh until some unrelated later change fixes it by accident - the
    ///         running MCP server would keep answering from the previous project after a re-import.
    ///     </para>
    ///     <para>
    ///         The fix compares a generation stamp at commit time: a <c>SetGraph</c> that happened
    ///         while the capture was in flight already moved the generation on, so the capture's
    ///         commit is skipped instead of overwriting it - the caller still gets a valid (if
    ///         momentarily behind) answer, but the cache is left alone for the next call to redo
    ///         properly.
    ///     </para>
    ///     <para>
    ///         Runs many iterations because the window is narrow and the pre-fix implementation only
    ///         lost the race probabilistically - a single pass could pass by chance even with the bug
    ///         present. Measured against the pre-fix code, this reproduces the bug in roughly 80% of
    ///         iterations; the fix drives that to zero.
    ///     </para>
    /// </summary>
    [Test]
    public async Task ALostUpdateBetweenSetGraphAndAnInFlightCapture_MustNotResurrectTheOldSnapshot()
    {
        var graphA = new TestCodeGraph();
        graphA.CreateAssembly("A");
        var graphB = new TestCodeGraph();
        graphB.CreateAssembly("B");

        const int attempts = 500;

        for (var i = 0; i < attempts; i++)
        {
            // Known, clean state for this iteration: liveGraph=A, isStale=true. SetGraph always
            // forces staleness, regardless of what the previous iteration left behind.
            await _dispatcher.InvokeAsync(() => _provider.SetGraph(graphA));

            // isStale is true, so this triggers a real capture of graphA on the dispatcher thread.
            var capturingA = _provider.GetSnapshotAsync();

            // Queued at the same Background priority, right behind Capture in the dispatcher's own
            // queue - it runs immediately after Capture finishes, on the same thread, with no
            // thread-hop delay. The async continuation of GetSnapshotAsync, by contrast, has to hop
            // back onto some other thread before it can take the lock and commit - that gap is the
            // window this test tries to land in.
            var settingB = _dispatcher.InvokeAsync(() => _provider.SetGraph(graphB),
                DispatcherPriority.Background);

            await capturingA;
            await settingB;

            // Both operations have now DEFINITELY completed. Ask again: a correct implementation
            // sees isStale=true (SetGraph(graphB) forced it) and re-captures, returning graphB.
            var afterBoth = await _provider.GetSnapshotAsync();

            Assert.That(HoldsAssembly(afterBoth, "A"), Is.False,
                $"iteration {i}: the cache resurrected a snapshot of the graph from before the swap");
        }
    }
}
