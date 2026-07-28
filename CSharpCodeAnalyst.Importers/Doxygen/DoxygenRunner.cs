using System.IO;
using CSharpCodeAnalyst.Importers.Shared;

namespace CSharpCodeAnalyst.Importers.Doxygen;

/// <summary>
///     Runs doxygen (expected on the PATH) over a C++, Python or Java source directory to produce
///     the XML output consumed by <see cref="DoxygenXmlConverter" />. The Doxyfile is generated
///     into the working directory; all options not listed keep their doxygen defaults.
/// </summary>
internal static class DoxygenRunner
{
    private static readonly TimeSpan AvailabilityTimeout = TimeSpan.FromSeconds(10);

    public static bool IsDoxygenAvailable()
    {
        return ProcessRunner.IsAvailable("doxygen", ["--version"], AvailabilityTimeout);
    }

    /// <summary>
    ///     Returns the directory containing the generated XML (index.xml etc.).
    /// </summary>
    public static async Task<string> RunAsync(string sourceDirectory, string workingDirectory, string projectName,
        DoxygenLanguage language, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(workingDirectory);

        var doxyfilePath = Path.Combine(workingDirectory, "Doxyfile");
        await File.WriteAllTextAsync(doxyfilePath, CreateDoxyfile(sourceDirectory, workingDirectory, projectName, language),
            cancellationToken);

        var result = await ProcessRunner.RunAsync(new ProcessRunner.Options("doxygen", [doxyfilePath], workingDirectory),
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"doxygen exited with code {result.ExitCode}. {result.ErrorTail}");
        }

        var xmlDirectory = Path.Combine(workingDirectory, "xml");
        if (!File.Exists(Path.Combine(xmlDirectory, "index.xml")))
        {
            throw new InvalidOperationException($"doxygen finished but produced no XML output in {xmlDirectory}. {result.ErrorTail}");
        }

        return xmlDirectory;
    }

    private static string CreateDoxyfile(string sourceDirectory, string outputDirectory, string projectName, DoxygenLanguage language)
    {
        // Python: keep virtual environments and caches out - a venv would drag the whole
        // installed package zoo into the graph and dwarf the actual project.
        // Java: the same for build output. Generated sources live below those directories
        // (target/generated-sources, build/generated), so they are excluded with them.
        // OPTIMIZE_OUTPUT_JAVA only changes the wording of the generated documentation, the XML
        // is byte-identical either way. It is set because it is the documented mode for Java.
        var (filePatterns, excludePatterns, languageOptions) = language switch
        {
            DoxygenLanguage.Python => ("*.py *.pyw", "*/venv/* */.venv/* */env/* */__pycache__/* */site-packages/* */.tox/*",
                string.Empty),
            DoxygenLanguage.Java => ("*.java", "*/build/* */target/* */out/* */bin/* */.gradle/* */.idea/*",
                "OPTIMIZE_OUTPUT_JAVA   = YES"),
            _ => ("*.h *.hh *.hpp *.hxx *.c *.cc *.cpp *.cxx", string.Empty, string.Empty)
        };

        // EXTRACT_* = YES: take everything, not just documented code.
        // REFERENCES_RELATION = YES: produces the <references> entries (call/use edges).
        return $"""
                PROJECT_NAME           = "{projectName}"
                INPUT                  = "{sourceDirectory}"
                RECURSIVE              = YES
                FILE_PATTERNS          = {filePatterns}
                EXCLUDE_PATTERNS       = {excludePatterns}
                {languageOptions}
                EXTRACT_ALL            = YES
                EXTRACT_PRIVATE        = YES
                EXTRACT_STATIC         = YES
                EXTRACT_ANON_NSPACES   = YES
                REFERENCES_RELATION    = YES
                REFERENCED_BY_RELATION = YES
                GENERATE_XML           = YES
                XML_PROGRAMLISTING     = NO
                GENERATE_HTML          = NO
                GENERATE_LATEX         = NO
                OUTPUT_DIRECTORY       = "{outputDirectory}"
                QUIET                  = YES
                WARN_IF_UNDOCUMENTED   = NO
                """;
    }
}
