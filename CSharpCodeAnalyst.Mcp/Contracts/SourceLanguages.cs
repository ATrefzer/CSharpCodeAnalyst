namespace CSharpCodeAnalyst.Mcp.Contracts;

/// <summary>
///     Names the languages a graph was built from.
///     <para>
///         The graph itself does not record one: the Roslyn parser and every importer produce the same
///         language neutral model, and <c>CodeElementType</c> is the same fixed set for all of them. The
///         file extensions are the only thing left that still says where the code came from - which is
///         enough, and costs nothing to read.
///     </para>
/// </summary>
internal static class SourceLanguages
{
    private static readonly Dictionary<string, string> ByExtension =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"] = "C#",
            [".dart"] = "Dart",
            [".py"] = "Python",
            [".pyi"] = "Python",
            [".java"] = "Java",
            [".cpp"] = "C++",
            [".cc"] = "C++",
            [".cxx"] = "C++",
            [".hpp"] = "C++",
            [".hxx"] = "C++",
            [".inl"] = "C++",
            [".c"] = "C/C++",
            [".h"] = "C/C++"
        };

    /// <summary>
    ///     One entry per language, with the number of distinct files behind it, most files first. Empty
    ///     when the graph carries no source locations at all - a plain text import, for instance, where
    ///     there never were any files.
    /// </summary>
    public static IReadOnlyList<(string Language, int Files)> Detect(CodeGraph.Graph.CodeGraph graph)
    {
        // A file holds many elements, and an element can be declared in several files. Counting
        // locations would therefore measure the size of the graph, not the size of the code base.
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in graph.Nodes.Values)
        {
            foreach (var location in element.SourceLocations)
            {
                if (!string.IsNullOrWhiteSpace(location.File))
                {
                    files.Add(location.File);
                }
            }
        }

        return files
            .GroupBy(NameLanguage, StringComparer.Ordinal)
            .Select(group => (Language: group.Key, Files: group.Count()))
            .OrderByDescending(entry => entry.Files)
            .ThenBy(entry => entry.Language, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    ///     An unknown extension is reported as itself rather than as "other": a caller that sees
    ///     <c>.kt</c> knows what it is looking at, and a caller that sees "other" knows nothing.
    /// </summary>
    private static string NameLanguage(string file)
    {
        var extension = Path.GetExtension(file);
        if (string.IsNullOrEmpty(extension))
        {
            return "unknown";
        }

        return ByExtension.TryGetValue(extension, out var language)
            ? language
            : extension.ToLowerInvariant();
    }
}
