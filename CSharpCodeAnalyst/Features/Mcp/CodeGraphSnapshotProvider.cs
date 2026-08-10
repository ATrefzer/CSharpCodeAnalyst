using System.Windows.Threading;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Mcp.Contracts;
using CSharpCodeAnalyst.Persistence.Contracts;

namespace CSharpCodeAnalyst.Features.Mcp;

/// <summary>
///     Hands the MCP server a copy of the loaded code graph instead of the graph itself.
///     <para>
///         The live graph is not safe to read from a request thread: the refactoring simulation mutates
///         it in place (move, delete, cut relationships) on the UI thread, and a query walking it at the
///         same time would see a half changed structure. Copying is what makes the two independent - the
///         copy belongs to the MCP layer alone, so no lock is needed on either side.
///     </para>
///     <para>
///         The copy is taken lazily. Loading a project or applying a refactoring only marks the current
///         one stale; the next tool call pays for the copy, and only if there was a change. That keeps
///         the cost off the interactive path, where nobody is waiting for a copy that may never be read.
///         The copy itself still runs on the UI thread - it is the one moment nothing may mutate - so a
///         large graph shows up as a brief pause. Everything after it runs on the request thread.
///     </para>
/// </summary>
public sealed class CodeGraphSnapshotProvider(Dispatcher dispatcher, IProjectService projectService)
    : ICodeGraphSnapshotSource
{
    /// <summary>
    ///     Serializes the copying, so several tool calls arriving at once do not each start one and
    ///     then throw all but the last away.
    /// </summary>
    private readonly SemaphoreSlim _captureGate = new(1, 1);

    private readonly object _sync = new();

    /// <summary>Set on the UI thread, read on the UI thread while copying.</summary>
    private CodeGraph.Graph.CodeGraph? _liveGraph;

    private bool _containsRefactorings;

    private GraphSnapshot? _snapshot;
    private bool _isStale = true;

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

            var captured = await dispatcher.InvokeAsync(Capture, DispatcherPriority.Background,
                cancellationToken);

            lock (_sync)
            {
                _snapshot = captured;
                _isStale = false;
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
        _containsRefactorings = false;
        MarkStale();
    }

    /// <summary>
    ///     The loaded graph was changed in place by a refactoring simulation. Call on the UI thread.
    ///     The flag is sticky until the next <see cref="SetGraph" />, because from here on the graph no
    ///     longer describes the code on disk and every answer derived from it has to say so.
    /// </summary>
    public void MarkRefactored()
    {
        _containsRefactorings = true;
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
        }
    }

    private void MarkStale()
    {
        lock (_sync)
        {
            _isStale = true;
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

        return new GraphSnapshot(
            _liveGraph.Clone(),
            projectService.CurrentFilePath ?? string.Empty,
            DateTimeOffset.UtcNow,
            _containsRefactorings);
    }
}
