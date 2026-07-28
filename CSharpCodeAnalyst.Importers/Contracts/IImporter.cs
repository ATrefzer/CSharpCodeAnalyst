using CSharpCodeAnalyst.CodeGraph.Contracts;

namespace CSharpCodeAnalyst.Importers.Contracts;

/// <summary>
///     One way of turning something into a <see cref="CodeGraph.Graph.CodeGraph" /> - a source
///     directory, a tool's output file, a saved graph.
///     Deliberately shaped like <c>IAnalyzer</c>: identity plus one method that does the work. The
///     one thing analyzers do not need is user input, and every importer needs different input - so
///     an importer brings its own configuration UI rather than declaring parameters. A declarative
///     parameter model was considered and rejected: it could not express validation like "is this
///     Dart project resolved?", which is the most valuable part of that particular dialog.
///     Registration happens in the host's ImporterManager; the ribbon binds to the list, so adding
///     an importer needs no XAML change.
/// </summary>
public interface IImporter
{
    /// <summary>
    ///     Stable identifier, used for command routing. Never shown to the user.
    /// </summary>
    string Id { get; }

    /// <summary>
    ///     Menu label, including its access key ("Import _Dart/Flutter project ...").
    /// </summary>
    string Name { get; }

    string Description { get; }

    /// <summary>
    ///     Whether the external prerequisite of this importer is present - doxygen on the PATH, a
    ///     Dart SDK, ... Checked before the dialog opens, so a missing tool produces a clear message
    ///     instead of a failure in the middle of the import.
    ///     <paramref name="unavailableReason" /> is the message to show; it is null when available.
    /// </summary>
    bool IsAvailable(out string? unavailableReason);

    /// <summary>
    ///     Asks the user for whatever this importer needs, then produces the graph. Returns null when
    ///     the user cancelled - that is not an error and must not be reported as one.
    ///     Exceptions are the host's business: it wraps the call, shows the message and keeps the
    ///     previously loaded graph.
    ///     <para>
    ///         Called on the UI thread, because the dialog has to be. Anything CPU-bound after that
    ///         belongs on a worker - wrap it in <c>Task.Run</c>, otherwise the window freezes for the
    ///         duration of the import. Awaiting an external process is fine as it is.
    ///     </para>
    /// </summary>
    Task<ParseResult?> ImportAsync(IImportContext context);
}
