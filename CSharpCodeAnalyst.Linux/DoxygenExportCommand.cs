using CSharpCodeAnalyst.CodeGraph.Export;
using CSharpCodeAnalyst.Importers.Doxygen;

namespace CSharpCodeAnalyst.Linux;

/// <summary>
///     <c>-doxygen</c>: runs doxygen over a source directory, converts the XML into a code graph and
///     writes it in the plain text graph format. Does exactly what the doxygen import in the
///     application does, minus the dialog - both use <see cref="DoxygenRunner" /> and
///     <see cref="DoxygenXmlConverter" />.
///     <para>
///         The plain text format is the transport because the application already reads it
///         (Import &gt; Plain text) and because it loses nothing here: it carries elements,
///         relationships, attributes and source locations, and doxygen reports no metrics.
///     </para>
/// </summary>
internal sealed class DoxygenExportCommand
{
    /// <summary>Produces the directory with doxygen's XML (index.xml etc.).</summary>
    internal delegate Task<string> DoxygenRun(DoxygenExportOptions options, string workingDirectory, CancellationToken cancellationToken);

    private readonly DoxygenExportOptions _options;
    private readonly TextWriter _output;
    private readonly DoxygenRun _runDoxygen;

    public DoxygenExportCommand(DoxygenExportOptions options, TextWriter output)
        : this(options, output, RunDoxygenAsync)
    {
    }

    /// <summary>The tests replace doxygen with prepared XML; everything else runs for real.</summary>
    internal DoxygenExportCommand(DoxygenExportOptions options, TextWriter output, DoxygenRun runDoxygen)
    {
        _options = options;
        _output = output;
        _runDoxygen = runDoxygen;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var keepWorkingDirectory = _options.WorkingDirectory is not null;
        var workingDirectory = _options.WorkingDirectory
                               ?? Path.Combine(Path.GetTempPath(), "CSharpCodeAnalyst.Linux_" + Guid.NewGuid().ToString("N"));

        try
        {
            _output.WriteLine($"Running doxygen ({_options.Language}) over {_options.SourceDirectory} ...");
            var xmlDirectory = await _runDoxygen(_options, workingDirectory, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            _output.WriteLine("Converting doxygen XML ...");
            var projectName = ToPlainTextName(_options.ProjectName);
            if (projectName != _options.ProjectName)
            {
                _output.WriteLine($"The project name '{_options.ProjectName}' contains whitespace, which the graph file cannot carry. Using '{projectName}'.");
            }

            var converter = new DoxygenXmlConverter(_options.HierarchyMode, _options.SourceDirectory);
            var graph = converter.Convert(xmlDirectory, projectName);

            var outputDirectory = Path.GetDirectoryName(_options.OutputFile);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            CodeGraphSerializer.SerializeToFile(graph, _options.OutputFile);

            _output.WriteLine(
                $"Wrote {graph.Nodes.Count} code elements and {graph.GetAllRelationships().Count()} relationships to {_options.OutputFile}");
            if (converter.SkippedUnresolvedReferences > 0)
            {
                _output.WriteLine($"Skipped {converter.SkippedUnresolvedReferences} references doxygen could not resolve to an element.");
            }

            if (keepWorkingDirectory)
            {
                _output.WriteLine($"doxygen output kept in {workingDirectory}");
            }
        }
        finally
        {
            if (!keepWorkingDirectory)
            {
                TryDelete(workingDirectory);
            }
        }
    }

    /// <summary>
    ///     The plain text format splits its lines at whitespace, so a value must not contain any
    ///     (see Documentation/plain-text-graph-format.md). The converter already strips whitespace
    ///     from every C++ name and directory segment ("Box&lt; int &gt;" becomes "Box&lt;int&gt;"); the
    ///     project name is the one value it takes over verbatim, because the application's own
    ///     import does not go through this format. A source directory like "My Project" is common
    ///     enough that this is not an edge case: without it, the root would come back as "My" and
    ///     every full name below it would be cut off at the space. Same rule as the converter's,
    ///     so the result reads like the rest of the graph.
    /// </summary>
    internal static string ToPlainTextName(string name)
    {
        return string.Concat(name.Where(c => !char.IsWhiteSpace(c)));
    }

    private static Task<string> RunDoxygenAsync(DoxygenExportOptions options, string workingDirectory, CancellationToken cancellationToken)
    {
        if (!DoxygenRunner.IsDoxygenAvailable())
        {
            throw new InvalidOperationException("doxygen was not found on the PATH. On Ubuntu: sudo apt install doxygen");
        }

        return DoxygenRunner.RunAsync(options.SourceDirectory, workingDirectory, options.ProjectName, options.Language, cancellationToken);
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A leftover in the temp directory is not worth failing a run that produced its output.
        }
    }
}
