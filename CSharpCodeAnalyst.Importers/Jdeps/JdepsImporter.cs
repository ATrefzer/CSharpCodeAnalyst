using CSharpCodeAnalyst.CodeGraph.Contracts;
using CSharpCodeAnalyst.CodeGraph.Metrics;
using CSharpCodeAnalyst.Importers.Contracts;
using CSharpCodeAnalyst.Importers.Resources;

namespace CSharpCodeAnalyst.Importers.Jdeps;

/// <summary>
///     Imports the output of the JDK's jdeps tool. Unlike the other importers this one does not run
///     anything itself - the user produces the file with jdeps and picks it here.
/// </summary>
public sealed class JdepsImporter : IImporter
{
    public string Id
    {
        get => "jdeps";
    }

    public string Name
    {
        get => Strings.ImportJdeps_Label;
    }

    public string Description
    {
        get => Strings.ImportJdeps_Description;
    }

    public bool IsAvailable(out string? unavailableReason)
    {
        // Reading a file needs no external tool.
        unavailableReason = null;
        return true;
    }

    public Task<ParseResult?> ImportAsync(IImportContext context)
    {
        var path = context.UserNotification.ShowOpenFileDialog(Strings.ImportJdeps_FileFilter, Strings.ImportJdeps_DialogTitle);
        if (string.IsNullOrEmpty(path))
        {
            return Task.FromResult<ParseResult?>(null);
        }

        var graph = new JdepsReader().ImportFromFile(path);
        return Task.FromResult<ParseResult?>(new ParseResult(graph, new MetricStore()));
    }
}
