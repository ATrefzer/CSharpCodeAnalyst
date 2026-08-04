using System.IO;
using System.Windows;
using CSharpCodeAnalyst.CodeGraph.Contracts;
using CSharpCodeAnalyst.CodeGraph.Metrics;
using CSharpCodeAnalyst.Importers.Contracts;
using CSharpCodeAnalyst.Importers.Resources;

namespace CSharpCodeAnalyst.Importers.Doxygen;

/// <summary>
///     Imports a C++, Python or Java project by running doxygen (expected on the PATH) over a source
///     directory and converting its XML output. The wizard only asks for the directory, the language
///     and a project name; everything else happens in the background.
/// </summary>
public sealed class DoxygenImporter : IImporter
{
    public string Id
    {
        get => "doxygen";
    }

    public string Name
    {
        get => Strings.ImportDoxygen_Label;
    }

    public string Description
    {
        get => Strings.ImportDoxygen_Description;
    }

    public bool IsAvailable(out string? unavailableReason)
    {
        if (DoxygenRunner.IsDoxygenAvailable())
        {
            unavailableReason = null;
            return true;
        }

        unavailableReason = Strings.ImportDoxygen_DoxygenNotFound;
        return false;
    }

    public async Task<ParseResult?> ImportAsync(IImportContext context)
    {
        var viewModel = new DoxygenImportDialogViewModel();
        var dialog = new DoxygenImportDialog(viewModel, context.UserNotification) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var projectName = viewModel.ProjectName.Trim();
        var sourceDirectory = viewModel.SourceDirectory;
        var language = viewModel.SelectedLanguage.Value;
        var hierarchyMode = viewModel.SelectedHierarchyMode.Value;

        return await Task.Run(async () =>
        {
            context.Progress.Report(Strings.ImportDoxygen_RunningDoxygen);
            var xmlDirectory = await DoxygenRunner.RunAsync(sourceDirectory, context.WorkingDirectory, projectName, language,
                context.CancellationToken);

            context.Progress.Report(Strings.ImportDoxygen_Converting);
            var graph = new DoxygenXmlConverter(hierarchyMode, sourceDirectory).Convert(xmlDirectory, projectName);

            // doxygen reports no source metrics.
            return (ParseResult?)new ParseResult(graph, new MetricStore());
        }, context.CancellationToken);
    }
}
