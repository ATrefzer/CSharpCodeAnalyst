using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.CodeGraph.Contracts;
using CSharpCodeAnalyst.CodeParser.Parser;
using CSharpCodeAnalyst.CodeParser.Parser.Config;
using CSharpCodeAnalyst.Importers.Contracts;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared;

namespace CSharpCodeAnalyst.Features.Import;

/// <summary>
///     Drives the imports.
///     The C# solution import is implemented here because it takes its options from the settings
///     rather than from a dialog. Everything else is an <see cref="IImporter" /> from
///     CSharpCodeAnalyst.Importers and goes through <see cref="RunImporterAsync" />, which supplies
///     the context and owns busy state, cancellation and error reporting.
/// </summary>
public class Importer
{
    /// <summary>
    ///     Busy/status-bar sink, owned by MainViewModel and injected here.
    /// </summary>
    private readonly IProgress<BusyState> _busy;

    /// <summary>
    ///     Wraps <see cref="_busy" /> for the parser and the importers, which report plain progress
    ///     text. Constructed once on the UI thread, so it captures the UI SynchronizationContext:
    ///     progress reported from the background run (see ExecuteGuardedImportAsync) is marshalled
    ///     back automatically instead of touching view-model properties from a worker thread.
    /// </summary>
    private readonly IProgress<string> _progress;

    private readonly IUserNotification _ui;

    /// <summary>
    ///     Store this value because we cannot show the diagnostics dialog in the worker.
    /// </summary>
    private IParserDiagnostics? _parserDiagnostics;

    public Importer(IUserNotification ui, IProgress<BusyState> busy)
    {
        _ui = ui;
        _busy = busy;
        _progress = new Progress<string>(msg => _busy.Report(new BusyState(msg, true)));
    }

    public async Task<Result<ParseResult>> ImportSolutionAsync(ProjectExclusionRegExCollection filters, bool includeExternalCode)
    {
        var fileName = TryGetImportSolutionPath();
        if (string.IsNullOrEmpty(fileName))
        {
            return Result<ParseResult>.Canceled();
        }

        var result = await ExecuteGuardedImportAsync(
            Strings.Load_Message_Default,
            async () => (ParseResult?)await Task.Run(() =>
                ImportSolutionFuncAsync(fileName, filters, includeExternalCode)));

        if (_parserDiagnostics is { HasDiagnostics: true })
        {
            _ui.ShowErrorWarningDialog(_parserDiagnostics.Failures, _parserDiagnostics.Warnings);
        }

        return result;
    }

    /// <summary>
    ///     Runs one importer: checks its prerequisite, lets it ask the user for whatever it needs,
    ///     and executes it off the UI thread. A null result means the user cancelled - that is not an
    ///     error and leaves the currently loaded graph alone.
    /// </summary>
    public async Task<Result<ParseResult>> RunImporterAsync(IImporter importer)
    {
        if (!importer.IsAvailable(out var unavailableReason))
        {
            _ui.ShowError(unavailableReason ?? string.Empty);
            return Result<ParseResult>.Canceled();
        }

        using var context = new ImportContext(_ui, _progress, importer);

        // Called on the UI thread because the importer opens its own dialog; moving the actual work
        // to a worker is the importer's job (see IImporter.ImportAsync).
        return await ExecuteGuardedImportAsync(Strings.Import_Progress, () => importer.ImportAsync(context));
    }

    private async Task<ParseResult> ImportSolutionFuncAsync(string solutionPath, ProjectExclusionRegExCollection filters,
        bool includeExternalCode)
    {
        var parser = new Parser(new ParserConfig(filters, includeExternalCode), _progress);

        _parserDiagnostics = null;
        var parseResult = await parser.ParseAsync(solutionPath).ConfigureAwait(true);

        if (parser.Diagnostics.HasDiagnostics)
        {
            _parserDiagnostics = parser.Diagnostics;
        }

        return parseResult;
    }

    private async Task<Result<ParseResult>> ExecuteGuardedImportAsync(string progressMessage, Func<Task<ParseResult?>> importFunc)
    {
        try
        {
            _busy.Report(new BusyState(progressMessage, true));

            var parseResult = await importFunc();
            return parseResult is null
                ? Result<ParseResult>.Canceled()
                : Result<ParseResult>.Success(parseResult);
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            _ui.ShowError(message);
            return Result<ParseResult>.Failure(ex);
        }
        finally
        {
            _busy.Report(new BusyState(string.Empty, false));
        }
    }

    private string? TryGetImportSolutionPath()
    {
        return _ui.ShowOpenFileDialog(Strings.Import_FileFilter, Strings.Import_DialogTitle);
    }
}
