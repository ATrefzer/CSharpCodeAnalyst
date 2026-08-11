using System.ComponentModel;
using System.Globalization;
using System.Text;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Mcp.Contracts;
using ModelContextProtocol.Server;

namespace CSharpCodeAnalyst.Mcp.Tools;

/// <summary>
///     Tells a caller what it is actually looking at before it starts asking questions about it.
/// </summary>
[McpServerToolType]
public sealed class GraphInfoTools(ICodeGraphSnapshotSource snapshotSource)
{
    [McpServerTool(Name = "graph_info", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Describes the code graph currently loaded in CSharp Code Analyst: which languages it was " +
        "built from, how large it is, which kinds of element it holds and which assemblies it " +
        "contains. Call this first - the loaded code base can be C#, C++, Dart, Python or Java, and " +
        "this is the only way to find out which. It also reports the source root the file locations " +
        "in every other answer are relative to, which is what turns one of those locations into a " +
        "path you can open.")]
    public async Task<string> GraphInfoAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshotSource.GetSnapshotAsync(cancellationToken);
        if (snapshot is null)
        {
            return ToolText.NoProjectLoaded;
        }

        var graph = snapshot.Graph;
        var text = new StringBuilder();

        // First line, because it is the one fact the rest of an answer cannot be read without. The
        // application is named after C#, and the tools are usually registered under a name that is
        // too - neither says anything about what is loaded right now.
        AppendLanguages(text, graph);

       
        // If you make code changes re-import and restart the mcp server.
        text.AppendLine(
            "Snapshot of the code as it was last analyzed in the application. Edits made on disk " +
            "since then are not in it; re-analyze there if an answer looks out of date.");

        // Without the root, a relative location cannot be turned back into a file: the caller's
        // working directory is not necessarily the one the graph was parsed in, and need not even be
        // on the same machine. Said either way round: a caller told that a root exists, and then not
        // given one, has to guess whether the locations it sees are relative or absolute.
        if (!string.IsNullOrEmpty(snapshot.SourceRoot))
        {
            text.Append("Source root: ").AppendLine(snapshot.SourceRoot);
            text.AppendLine("File locations in every answer are relative to it.");
        }
        else
        {
            text.AppendLine(
                "Source root: none - the files share no common directory, so every answer reports " +
                "full paths.");
        }

        text.Append("Code elements: ")
            .AppendLine(graph.Nodes.Count.ToString(CultureInfo.InvariantCulture));
        text.Append("Relationships: ")
            .AppendLine(graph.GetAllRelationships().Count().ToString(CultureInfo.InvariantCulture));

        AppendKinds(text, graph);

        AppendAssemblies(text, graph);

        text.AppendLine();
        text.AppendLine(
            "Element ids are opaque and valid only while this server runs. Use search_elements to " +
            "find an element and obtain its id.");

        return text.ToString();
    }

    private static void AppendLanguages(StringBuilder text, CodeGraph.Graph.CodeGraph graph)
    {
        var languages = SourceLanguages.Detect(graph);
        if (languages.Count == 0)
        {
            // An import that carries no file locations. Saying nothing here would leave the language
            // to be guessed from the assembly names, which is exactly what this line exists to stop.
            text.AppendLine("Languages: unknown - this graph carries no file locations.");
            return;
        }

        var formatted = languages.Select(entry =>
            $"{entry.Language} ({entry.Files.ToString(CultureInfo.InvariantCulture)} " +
            $"{(entry.Files == 1 ? "file" : "files")})");

        text.Append("Languages: ").AppendLine(string.Join(", ", formatted));
    }

    /// <summary>
    ///     Which kinds of element the graph actually holds, and in what proportion. Answers two
    ///     questions at once: what a code base is made of, and which <c>type:</c> filters are worth
    ///     spending a search on - the kind names are exactly the values that filter accepts, and a kind
    ///     missing here will never match. That matters for an imported graph, where several kinds of the
    ///     model simply never occur.
    /// </summary>
    private static void AppendKinds(StringBuilder text, CodeGraph.Graph.CodeGraph graph)
    {
        if (graph.Nodes.Count == 0)
        {
            return;
        }

        text.Append("Kinds: ")
            .AppendLine(ElementFormatter.Summarize(graph.Nodes.Values,
                element => element.ElementType.ToString()));
        text.AppendLine("Each name is a valid 'type:<kind>' filter in search_elements.");
    }

    private static void AppendAssemblies(StringBuilder text, CodeGraph.Graph.CodeGraph graph)
    {
        // Roots are the assemblies: the parser puts everything below one, inserting a synthetic
        // namespace where code sits at the root, so nothing else ends up parentless.
        var assemblies = graph.GetRoots()
            .Where(root => root.ElementType == CodeElementType.Assembly)
            .OrderBy(root => root.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (assemblies.Count == 0)
        {
            return;
        }

        text.AppendLine();

        // "Assembly" is what the model calls the top level, because it grew up on C#. For an imported
        // graph the same node is a package, a module or a jar, and a caller that takes the word
        // literally will look for something the code base does not have.
        text.Append("Assemblies (").Append(assemblies.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(") - the roots of the graph. Depending on where the code came from, one is an " +
                        "assembly, a package or a module:");
        foreach (var assembly in assemblies)
        {
            text.Append("  ").Append(assembly.Name);
            if (assembly.IsExternal)
            {
                text.Append("  [external]");
            }

            text.AppendLine();
        }
    }
}
