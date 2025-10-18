using System.Windows;
using CodeParser.Parser;
using CodeParser.Parser.Config;
using Contracts.Common;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Resources;
using Microsoft.Win32;

namespace CSharpCodeAnalyst.Import;

/// <summary>
///     Imports various file format into a CodeGraph.
/// </summary>
public class Importer(ApplicationSettings applicationSettings)
{
    /// <summary>
    ///     Store this value because we cannot show the diagnostics dialog in the worker.
    /// </summary>
    private IParserDiagnostics? _parserDiagnostics;

    public event EventHandler<ImportStateChangedArgs>? ImportStateChanged;

    public async Task<Result<CodeGraph>> ImportSolutionAsync(ProjectExclusionRegExCollection filters)
    {
        var fileName = TryGetImportSolutionPath();
        if (string.IsNullOrEmpty(fileName))
        {
            return Result<CodeGraph>.Canceled();
        }

        var result = await ExecuteGuardedImportAsync(
            Strings.Load_Message_Default,
            () => ImportSolutionFuncAsync(fileName, filters));

        if (_parserDiagnostics is { HasDiagnostics: true })
        {
            ErrorWarningDialog.Show(_parserDiagnostics.Failures, _parserDiagnostics.Warnings, Application.Current.MainWindow);
        }


        return result;
    }

    public async Task<Result<CodeGraph>> ImportJdepsAsync()
    {
        var fileName = TryGetImportJdepsFilePath();
        if (string.IsNullOrEmpty(fileName))
        {
            return Result<CodeGraph>.Canceled();
        }

        return await ExecuteGuardedImportAsync(
            "Importing jdeps data...",
            () => ImportJDepsFuncAsync(fileName));
    }

    public async Task<Result<CodeGraph>> ImportPlainTextAsync()
    {
        var fileName = TryGetImportPlainTextPath();
        if (string.IsNullOrEmpty(fileName))
        {
            return Result<CodeGraph>.Canceled();
        }

        return await ExecuteGuardedImportAsync(
            "Importing plain text graph...",
            () => ImportPlainTextFuncAsync(fileName));
    }

    private Task<CodeGraph> ImportJDepsFuncAsync(string filePath)
    {
        var importer = new JdepsReader();
        return Task.FromResult(importer.ImportFromFile(filePath));
    }

    private Task<CodeGraph> ImportPlainTextFuncAsync(string filePath)
    {
        var graph = CodeGraphSerializer.DeserializeFromFile(filePath);
        return Task.FromResult(graph);
    }


    private async Task<CodeGraph> ImportSolutionFuncAsync(string solutionPath, ProjectExclusionRegExCollection filters)
    {
        var parser = new Parser(new ParserConfig(filters, applicationSettings.IncludeExternalCode));
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

    private async Task<Result<CodeGraph>> ExecuteGuardedImportAsync(string progressMessage, Func<Task<CodeGraph>> importFunc)
    {
        try
        {
            OnImportStateChanged(progressMessage, true);

            var graph = await Task.Run(importFunc);
            return Result<CodeGraph>.Success(graph);
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return Result<CodeGraph>.Failure(ex);
        }
        finally
        {
            OnImportStateChanged(string.Empty, false);
        }
    }

    private string? TryGetImportJdepsFilePath()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Select jdeps output file"
        };

        return ShowOpenFileDialog(openFileDialog);
    }

    private static string? TryGetImportSolutionPath()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = Strings.Import_FileFilter,
            Title = Strings.Import_DialogTitle
        };

        return ShowOpenFileDialog(openFileDialog);
    }

    private static string? TryGetImportPlainTextPath()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Select plaint text graph file"
        };

        return ShowOpenFileDialog(openFileDialog);
    }

    private static string? ShowOpenFileDialog(OpenFileDialog openFileDialog)
    {
        if (openFileDialog.ShowDialog() != true)
        {
            return null;
        }

        return openFileDialog.FileName;
    }
}