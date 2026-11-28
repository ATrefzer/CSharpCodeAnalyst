using System.Diagnostics.CodeAnalysis;
using CSharpCodeAnalyst.Importers.Doxygen;

namespace CSharpCodeAnalyst.Linux;

/// <summary>
///     What <c>-doxygen</c> was asked to do. The same choices the import dialog offers, with the
///     same defaults, so a graph written here matches the one an interactive import would produce.
/// </summary>
internal sealed record DoxygenExportOptions(
    string SourceDirectory,
    string OutputFile,
    DoxygenLanguage Language,
    DoxygenHierarchyMode HierarchyMode,
    string ProjectName,
    string? WorkingDirectory)
{
    private static readonly HashSet<string> KnownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "doxygen", "source", "out", "language", "hierarchy", "name", "work-dir"
    };

    public static bool TryParse(Arguments arguments, [NotNullWhen(true)] out DoxygenExportOptions? options,
        [NotNullWhen(false)] out string? problem)
    {
        options = null;

        var unknown = arguments.Keys.Where(key => !KnownKeys.Contains(key)).ToList();
        if (unknown.Count > 0)
        {
            problem = $"Unknown argument(s): {string.Join(", ", unknown.Select(key => "-" + key))}.";
            return false;
        }

        var source = arguments.Get("source");
        if (source is null)
        {
            problem = "-source:<dir> is required.";
            return false;
        }

        source = Path.GetFullPath(source);
        if (!Directory.Exists(source))
        {
            problem = $"The source directory '{source}' does not exist.";
            return false;
        }

        var output = arguments.Get("out");
        if (output is null)
        {
            problem = "-out:<file> is required.";
            return false;
        }

        var language = DoxygenLanguage.Cpp;
        var languageText = arguments.Get("language");
        if (languageText is not null && !TryParseLanguage(languageText, out language))
        {
            problem = $"Unknown language '{languageText}'. Use cpp, python or java.";
            return false;
        }

        var hierarchy = DoxygenHierarchyMode.Declared;
        var hierarchyText = arguments.Get("hierarchy");
        if (hierarchyText is not null && !Enum.TryParse(hierarchyText, true, out hierarchy))
        {
            problem = $"Unknown hierarchy '{hierarchyText}'. Use declared or directories.";
            return false;
        }

        var name = arguments.Get("name")?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            // Same suggestion as the import dialog: the name of the directory itself.
            name = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        if (string.IsNullOrEmpty(name))
        {
            // The source is a drive or file system root.
            problem = "Cannot derive a project name from the source directory. Pass -name:<name>.";
            return false;
        }

        var workingDirectory = arguments.Get("work-dir");
        options = new DoxygenExportOptions(source, Path.GetFullPath(output), language, hierarchy, name,
            workingDirectory is null ? null : Path.GetFullPath(workingDirectory));
        problem = null;
        return true;
    }

    private static bool TryParseLanguage(string text, out DoxygenLanguage language)
    {
        switch (text.ToLowerInvariant())
        {
            case "cpp":
            case "c++":
            case "c":
                language = DoxygenLanguage.Cpp;
                return true;
            case "python":
            case "py":
                language = DoxygenLanguage.Python;
                return true;
            case "java":
                language = DoxygenLanguage.Java;
                return true;
            default:
                language = default;
                return false;
        }
    }
}
