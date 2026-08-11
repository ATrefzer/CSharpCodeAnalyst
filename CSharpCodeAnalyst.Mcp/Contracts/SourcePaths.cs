namespace CSharpCodeAnalyst.Mcp.Contracts;

/// <summary>
///     Turns the absolute source paths of a graph into short, still unambiguous ones.
/// </summary>
internal static class SourcePaths
{
    private static readonly char[] Separators = ['\\', '/'];

    /// <summary>
    ///     The deepest directory shared by every source file in the graph, or <c>null</c> when there is
    ///     none - an empty graph, a graph without locations, or one spanning two drives. Callers treat
    ///     null as "report the full path": without a common prefix there is no redundancy to remove.
    ///     Computed once per snapshot rather than per answer.
    /// </summary>
    public static string? FindRoot(CodeGraph.Graph.CodeGraph graph)
    {
        string[]? common = null;
        var separator = Path.DirectorySeparatorChar;

        foreach (var element in graph.Nodes.Values)
        {
            foreach (var location in element.SourceLocations)
            {
                var file = location.File;
                if (string.IsNullOrWhiteSpace(file))
                {
                    continue;
                }

                // A file name without any directory
                var directory = SplitDirectory(file);
                if (directory.Length == 0)
                {
                    continue;
                }

                if (common is null)
                {
                    common = directory;

                    // Imported graphs may use the other separator.
                    separator = file.Contains('\\') ? '\\' : '/';
                    continue;
                }

                common = CommonPrefix(common, directory);
                if (common.Length == 0)
                {
                    return null;
                }
            }
        }

        // All-empty segments mean the paths share nothing but a leading separator. Stripping that
        // would turn absolute paths into ones that look relative, which is worse than leaving them.
        if (common is null || common.All(string.IsNullOrEmpty))
        {
            return null;
        }

        return string.Join(separator, common);
    }

    /// <summary>
    ///     The path with <paramref name="root" /> removed. Falls back to the unchanged path whenever
    ///     that cannot be done - no root, or a file outside it. The full path is long but never wrong,
    ///     and a location a caller cannot open is worth nothing.
    /// </summary>
    public static string MakeRelative(string file, string? root)
    {
        if (string.IsNullOrEmpty(root) || file.Length <= root.Length ||
            !file.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return file;
        }

        return file[root.Length..].TrimStart(Separators);
    }

    /// <summary>
    ///     The directory part, as segments. Empty segments are kept: they carry the leading separators
    ///     of a UNC path, and dropping them would produce a root that no longer prefixes the paths it
    ///     came from.
    /// </summary>
    private static string[] SplitDirectory(string file)
    {
        var segments = file.Split(Separators);
        return segments.Length <= 1 ? [] : segments[..^1];
    }

    private static string[] CommonPrefix(string[] left, string[] right)
    {
        var length = Math.Min(left.Length, right.Length);
        var shared = 0;

        while (shared < length &&
               string.Equals(left[shared], right[shared], StringComparison.OrdinalIgnoreCase))
        {
            shared++;
        }

        return left[..shared];
    }
}