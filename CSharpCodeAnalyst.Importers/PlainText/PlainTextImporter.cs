using CSharpCodeAnalyst.CodeGraph.Contracts;
using CSharpCodeAnalyst.CodeGraph.Export;
using CSharpCodeAnalyst.CodeGraph.Metrics;
using CSharpCodeAnalyst.Importers.Contracts;
using CSharpCodeAnalyst.Importers.Resources;

namespace CSharpCodeAnalyst.Importers.PlainText;

/// <summary>
///     Reads back a graph written in the plain text format (see
///     Documentation/plain-text-graph-format.md). The counterpart of the plain text export, and the
///     way to hand-write a graph or produce one from a tool we have no importer for.
/// </summary>
public sealed class PlainTextImporter : IImporter
{
    public string Id
    {
        get => "plaintext";
    }

    public string Name
    {
        get => Strings.ImportPlainText_Label;
    }

    public string Description
    {
        get => Strings.ImportPlainText_Description;
    }

    public bool IsAvailable(out string? unavailableReason)
    {
        unavailableReason = null;
        return true;
    }

    public Task<ParseResult?> ImportAsync(IImportContext context)
    {
        var path = context.UserNotification.ShowOpenFileDialog(Strings.ImportPlainText_FileFilter, Strings.ImportPlainText_DialogTitle);
        if (string.IsNullOrEmpty(path))
        {
            return Task.FromResult<ParseResult?>(null);
        }

        var graph = CodeGraphSerializer.DeserializeFromFile(path);
        return Task.FromResult<ParseResult?>(new ParseResult(graph, new MetricStore()));
    }
}
