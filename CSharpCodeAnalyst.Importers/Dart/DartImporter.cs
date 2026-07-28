using System.Windows;
using CSharpCodeAnalyst.CodeGraph.Contracts;
using CSharpCodeAnalyst.Importers.Contracts;
using CSharpCodeAnalyst.Importers.Resources;

namespace CSharpCodeAnalyst.Importers.Dart;

/// <summary>
///     Imports a Dart or Flutter project by running the bundled DartExtractor tool (which uses the
///     Dart analyzer) over the project directory. The wizard only asks for the directory - package
///     names and the file layout give the graph its structure.
/// </summary>
public sealed class DartImporter : IImporter
{
    public string Id
    {
        get => "dart";
    }

    public string Name
    {
        get => Strings.ImportDart_Label;
    }

    public string Description
    {
        get => Strings.ImportDart_Description;
    }

    public bool IsAvailable(out string? unavailableReason)
    {
        if (DartRunner.FindDartExecutable() is not null)
        {
            unavailableReason = null;
            return true;
        }

        unavailableReason = Strings.ImportDart_DartNotFound;
        return false;
    }

    public async Task<ParseResult?> ImportAsync(IImportContext context)
    {
        var viewModel = new DartImportDialogViewModel();
        var dialog = new DartImportDialog(viewModel, context.UserNotification) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var projectDirectory = viewModel.ProjectDirectory;

        return await Task.Run(async () =>
        {
            var jsonPath = await DartRunner.RunAsync(projectDirectory, context.WorkingDirectory, context.AssetDirectory,
                context.Progress, context.CancellationToken);

            context.Progress.Report(Strings.ImportDart_Converting);
            var converter = new DartGraphConverter();
            var graph = converter.ConvertFile(jsonPath);
            return (ParseResult?)new ParseResult(graph, converter.Metrics);
        }, context.CancellationToken);
    }
}
