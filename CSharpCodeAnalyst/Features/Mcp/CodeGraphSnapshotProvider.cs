using System.Windows.Threading;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Mcp.Contracts;
using CSharpCodeAnalyst.Persistence.Contracts;

namespace CSharpCodeAnalyst.Features.Mcp;

/// <summary>
/// <inheritdoc/>
/// </summary>
public sealed class CodeGraphSnapshotProvider(Dispatcher dispatcher)
    : ICodeGraphSnapshotSource
{
    /// <summary>
    ///     Serializes the copying, so several tool calls arriving at once do not each start one and
    ///     then throw all but the last away.
    /// </summary>
    private readonly SemaphoreSlim _captureGate = new(1, 1);

    private readonly Lock _sync = new();

    /// <summary>Set on the UI thread, read on the UI thread while copying.</summary>
    private CodeGraph.Graph.CodeGraph? _liveGraph;

    private GraphSnapshot? _snapshot;
    private bool _isStale = true;

    /// <summary>
    ///     Bumped every time <see cref="_isStale" /> is forced true - i.e. every time the live graph
    ///     might have moved on. Guards against a lost update: capturing runs on the UI thread and takes
    ///     real time for a large graph, so a <see cref="SetGraph" /> (or <see cref="Release" />) can land
    ///     while a capture is already in flight. Without this, whichever of the two writers reaches the
    ///     lock last below wins - and if that happens to be the in-flight capture, it commits a snapshot
    ///     of the graph as it was *before* the swap and marks it not-stale, silently resurrecting stale
    ///     data that nothing will refresh until the *next* unrelated change happens to fix it by
    ///     accident. Comparing the generation at commit time turns that race into a no-op instead: the
    ///     caller still gets its answer, but the cache is left alone for the next call to redo properly.
    /// </summary>
    private int _generation;

    public async Task<GraphSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (TryGetCurrent(out var current))
        {
            return current;
        }

        await _captureGate.WaitAsync(cancellationToken);
        try
        {
            // Another call may have captured while this one waited for the gate.
            if (TryGetCurrent(out current))
            {
                return current;
            }

            int generationAtStart;
            lock (_sync)
            {
                generationAtStart = _generation;
            }

            var captured = await dispatcher.InvokeAsync(Capture, DispatcherPriority.Background,
                cancellationToken);

            lock (_sync)
            {
                if (_generation == generationAtStart)
                {
                    _snapshot = captured;
                    _isStale = false;
                }
            }

            return captured;
        }
        finally
        {
            _captureGate.Release();
        }
    }

    /// <summary>
    ///     A different graph is now the loaded one. Call on the UI thread whenever the application
    ///     swaps graphs - importing a solution, loading a project, restoring a snapshot.
    /// </summary>
    public void SetGraph(CodeGraph.Graph.CodeGraph graph)
    {
        _liveGraph = graph;
        MarkStale();
    }

    /// <summary>
    ///     Throws the copy away. Called when the server stops: switching the feature off should also
    ///     give back the memory it costs, and nothing is left to answer a question with anyway.
    /// </summary>
    public void Release()
    {
        lock (_sync)
        {
            _snapshot = null;
            _isStale = true;
            _generation++;
        }
    }

    private void MarkStale()
    {
        lock (_sync)
        {
            _isStale = true;
            _generation++;
        }
    }

    /// <summary>
    ///     Note the distinction between "no snapshot" and "a snapshot whose value is null": with no
    ///     project loaded the captured value is legitimately null, and returning true for it keeps the
    ///     idle application from copying nothing on every single call.
    /// </summary>
    private bool TryGetCurrent(out GraphSnapshot? snapshot)
    {
        lock (_sync)
        {
            snapshot = _snapshot;
            return !_isStale;
        }
    }

    private GraphSnapshot? Capture()
    {
        if (_liveGraph is null)
        {
            return null;
        }

        return new GraphSnapshot(_liveGraph.Clone());
    }
}
