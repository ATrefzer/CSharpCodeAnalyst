using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.CodeGraph.Contracts;
using CSharpCodeAnalyst.CodeGraph.Export;
using CSharpCodeAnalyst.CodeGraph.Metrics;
using CSharpCodeAnalyst.CodeParser.Parser;
using CSharpCodeAnalyst.CodeParser.Parser.Config;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared;
using CSharpCodeAnalyst.Shared.Notifications;

namespace CSharpCodeAnalyst.Features.Import;

/// <summary>
///     Imports various file format into a CodeGraph.
/// </summary>
public class Importer
{
    private readonly IUserNotification _ui;

    /// <summary>
    ///     Constructed once on the UI thread, so it captures the UI SynchronizationContext: progress
    ///     reported from the background parse (see ExecuteGuardedImportAsync) is marshalled back
    ///     automatically instead of touching view-model properties from a worker thread.
    /// </summary>
    private readonly IProgress<string> _progress;

    /// <summary>
    ///     Store this value because we cannot show the diagnostics dialog in the worker.
    /// </summary>
    private IParserDiagnostics? _parserDiagnostics;

    public Importer(IUserNotification ui)
    {
        _ui = ui;
        _progress = new Progress<string>(msg => OnImportStateChanged(msg, true));
    }

    public event EventHandler<ImportStateChangedArgs>? ImportStateChanged;

    public async Task<Result<ParseResult>> ImportSolutionAsync(ProjectExclusionRegExCollection filters, bool includeExternalCode, bool includeGeneratedCode, bool splitPropertyAccessors)
    {
        var fileName = TryGetImportSolutionPath();
        if (string.IsNullOrEmpty(fileName))
        {
            return Result<ParseResult>.Canceled();
        }

        var result = await ExecuteGuardedImportAsync(
            Strings.Load_Message_Default,
            () => ImportSolutionFuncAsync(fileName, filters, includeExternalCode, includeGeneratedCode, splitPropertyAccessors));

        if (_parserDiagnostics is { HasDiagnostics: true })
        {
            _ui.ShowErrorWarningDialog(_parserDiagnostics.Failures, _parserDiagnostics.Warnings);
        }


        return result;
    }

    public async Task<Result<ParseResult>> ImportJdepsAsync()
    {
        var fileName = TryGetImportJdepsFilePath();
        if (string.IsNullOrEmpty(fileName))
        {
            return Result<ParseResult>.Canceled();
        }

        return await ExecuteGuardedImportAsync(
            "Importing jdeps data...",
            () => ImportJDepsFuncAsync(fileName));
    }

    public async Task<Result<ParseResult>> ImportPlainTextAsync()
    {
        var fileName = TryGetImportPlainTextPath();
        if (string.IsNullOrEmpty(fileName))
        {
            return Result<ParseResult>.Canceled();
        }

        return await ExecuteGuardedImportAsync(
            "Importing plain text graph...",
            () => ImportPlainTextFuncAsync(fileName));
    }

    private Task<ParseResult> ImportJDepsFuncAsync(string filePath)
    {
        var importer = new JdepsReader();
        return Task.FromResult(new ParseResult(importer.ImportFromFile(filePath), new MetricStore()));
    }

    private Task<ParseResult> ImportPlainTextFuncAsync(string filePath)
    {
        var graph = CodeGraphSerializer.DeserializeFromFile(filePath);
        return Task.FromResult(new ParseResult(graph, new MetricStore()));
    }


    private async Task<ParseResult> ImportSolutionFuncAsync(string solutionPath, ProjectExclusionRegExCollection filters, bool includeExternalCode, bool includeGeneratedCode, bool splitPropertyAccessors)
    {
        var parser = new Parser(new ParserConfig(filters, includeExternalCode, includeGeneratedCode, splitPropertyAccessors), _progress);

        _parserDiagnostics = null;
        var parseResult = await parser.ParseAsync(solutionPath).ConfigureAwait(true);

        if (parser.Diagnostics.HasDiagnostics)
        {
            _parserDiagnostics = parser.Diagnostics;
        }

        return parseResult;
    }

    private void OnImportStateChanged(string message, bool isLoading)
    {
        ImportStateChanged?.Invoke(this, new ImportStateChangedArgs(message, isLoading));
    }

    private async Task<Result<ParseResult>> ExecuteGuardedImportAsync(string progressMessage, Func<Task<ParseResult>> importFunc)
    {
        try
        {
            OnImportStateChanged(progressMessage, true);

            var parseResult = await Task.Run(importFunc);
            return Result<ParseResult>.Success(parseResult);
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            _ui.ShowError(message);
            return Result<ParseResult>.Failure(ex);
        }
        finally
        {
            OnImportStateChanged(string.Empty, false);
        }
    }

    private string? TryGetImportJdepsFilePath()
    {
        var filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
        var title = "Select jdeps output file";

        return _ui.ShowOpenFileDialog(filter, title);
    }

    private string? TryGetImportSolutionPath()
    {
        var filter = Strings.Import_FileFilter;
        var title = Strings.Import_DialogTitle;

        return _ui.ShowOpenFileDialog(filter, title);
    }

    private string? TryGetImportPlainTextPath()
    {
        var filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
        var title = "Select plaint text graph file";

        return _ui.ShowOpenFileDialog(filter, title);
    }
}