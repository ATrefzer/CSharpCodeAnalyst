using CodeGraph.Contracts;
using CodeGraph.Export;
using CodeParser.Parser;
using CodeParser.Parser.Config;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Import;

/// <summary>
///     Imports various file format into a CodeGraph.
/// </summary>
public class Importer
{
    private readonly IUserNotification _ui;

    /// <summary>
    ///     Store this value because we cannot show the diagnostics dialog in the worker.
    /// </summary>
    private IParserDiagnostics? _parserDiagnostics;

    public Importer(IUserNotification ui)
    {
        _ui = ui;
    }

    public event EventHandler<ImportStateChangedArgs>? ImportStateChanged;

    public async Task<Result<CodeGraph.Graph.CodeGraph>> ImportSolutionAsync(ProjectExclusionRegExCollection filters, bool includeExternalCode)
    {
        var fileName = TryGetImportSolutionPath();
        if (string.IsNullOrEmpty(fileName))
        {
            return Result<CodeGraph.Graph.CodeGraph>.Canceled();
        }

        var result = await ExecuteGuardedImportAsync(
            Strings.Load_Message_Default,
            () => ImportSolutionFuncAsync(fileName, filters, includeExternalCode));

        if (_parserDiagnostics is { HasDiagnostics: true })
        {
            _ui.ShowErrorWarningDialog(_parserDiagnostics.Failures, _parserDiagnostics.Warnings);
        }


        return result;
    }

    public async Task<Result<CodeGraph.Graph.CodeGraph>> ImportJdepsAsync()
    {
        var fileName = TryGetImportJdepsFilePath();
        if (string.IsNullOrEmpty(fileName))
        {
            return Result<CodeGraph.Graph.CodeGraph>.Canceled();
        }

        return await ExecuteGuardedImportAsync(
            "Importing jdeps data...",
            () => ImportJDepsFuncAsync(fileName));
    }

    public async Task<Result<CodeGraph.Graph.CodeGraph>> ImportPlainTextAsync()
    {
        var fileName = TryGetImportPlainTextPath();
        if (string.IsNullOrEmpty(fileName))
        {
            return Result<CodeGraph.Graph.CodeGraph>.Canceled();
        }

        return await ExecuteGuardedImportAsync(
            "Importing plain text graph...",
            () => ImportPlainTextFuncAsync(fileName));
    }

    private Task<CodeGraph.Graph.CodeGraph> ImportJDepsFuncAsync(string filePath)
    {
        var importer = new JdepsReader();
        return Task.FromResult(importer.ImportFromFile(filePath));
    }

    private Task<CodeGraph.Graph.CodeGraph> ImportPlainTextFuncAsync(string filePath)
    {
        var graph = CodeGraphSerializer.DeserializeFromFile(filePath);
        return Task.FromResult(graph);
    }


    private async Task<CodeGraph.Graph.CodeGraph> ImportSolutionFuncAsync(string solutionPath, ProjectExclusionRegExCollection filters, bool includeExternalCode)
    {
        var parser = new Parser(new ParserConfig(filters, includeExternalCode));
        parser.Progress.ParserProgress += OnParserProgress;

        try
        {
            _parserDiagnostics = null;
            var graph = await parser.ParseAsync(solutionPath).ConfigureAwait(true);

            if (parser.Diagnostics.HasDiagnostics)
            {
                _parserDiagnostics = parser.Diagnostics;
            }

            return graph;
        }
        finally
        {
            parser.Progress.ParserProgress -= OnParserProgress;
        }
    }

    private void OnParserProgress(object? sender, ParserProgressArg e)
    {
        OnImportStateChanged(e.Message, true);
    }

    private void OnImportStateChanged(string message, bool isLoading)
    {
        ImportStateChanged?.Invoke(this, new ImportStateChangedArgs(message, isLoading));
    }

    private async Task<Result<CodeGraph.Graph.CodeGraph>> ExecuteGuardedImportAsync(string progressMessage, Func<Task<CodeGraph.Graph.CodeGraph>> importFunc)
    {
        try
        {
            OnImportStateChanged(progressMessage, true);

            var graph = await Task.Run(importFunc);
            return Result<CodeGraph.Graph.CodeGraph>.Success(graph);
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            _ui.ShowError(message);
            return Result<CodeGraph.Graph.CodeGraph>.Failure(ex);
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