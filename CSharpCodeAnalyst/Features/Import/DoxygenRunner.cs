using System.Diagnostics;
using System.IO;

namespace CSharpCodeAnalyst.Features.Import;

/// <summary>
///     Runs doxygen (expected on the PATH) over a C++ or Python source directory to produce
///     the XML output consumed by <see cref="DoxygenXmlConverter" />. The Doxyfile is generated
///     into the working directory; all options not listed keep their doxygen defaults.
/// </summary>
internal static class DoxygenRunner
{
    public static bool IsDoxygenAvailable()
    {
        try
        {
            using var process = Process.Start(CreateStartInfo("--version"));
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(10000);
            return process is { HasExited: true, ExitCode: 0 };
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Returns the directory containing the generated XML (index.xml etc.).
    /// </summary>
    public static async Task<string> RunAsync(string sourceDirectory, string workingDirectory, string projectName, DoxygenLanguage language)
    {
        Directory.CreateDirectory(workingDirectory);

        var doxyfilePath = Path.Combine(workingDirectory, "Doxyfile");
        await File.WriteAllTextAsync(doxyfilePath, CreateDoxyfile(sourceDirectory, workingDirectory, projectName, language));

        var startInfo = CreateStartInfo($"\"{doxyfilePath}\"");
        startInfo.WorkingDirectory = workingDirectory;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start doxygen.");

        // Drain both streams while waiting, otherwise doxygen can block on a full pipe.
        var stdErrTask = process.StandardError.ReadToEndAsync();
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        await stdOutTask;

        var stdErr = await stdErrTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"doxygen exited with code {process.ExitCode}. {Tail(stdErr)}");
        }

        var xmlDirectory = Path.Combine(workingDirectory, "xml");
        if (!File.Exists(Path.Combine(xmlDirectory, "index.xml")))
        {
            throw new InvalidOperationException($"doxygen finished but produced no XML output in {xmlDirectory}. {Tail(stdErr)}");
        }

        return xmlDirectory;
    }

    private static ProcessStartInfo CreateStartInfo(string arguments)
    {
        return new ProcessStartInfo("doxygen", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
    }

    private static string CreateDoxyfile(string sourceDirectory, string outputDirectory, string projectName, DoxygenLanguage language)
    {
        // Python: keep virtual environments and caches out - a venv would drag the whole
        // installed package zoo into the graph and dwarf the actual project.
        var (filePatterns, excludePatterns) = language switch
        {
            DoxygenLanguage.Python => ("*.py *.pyw", "*/venv/* */.venv/* */env/* */__pycache__/* */site-packages/* */.tox/*"),
            _ => ("*.h *.hh *.hpp *.hxx *.c *.cc *.cpp *.cxx", string.Empty)
        };

        // EXTRACT_* = YES: take everything, not just documented code.
        // REFERENCES_RELATION = YES: produces the <references> entries (call/use edges).
        return $"""
                PROJECT_NAME           = "{projectName}"
                INPUT                  = "{sourceDirectory}"
                RECURSIVE              = YES
                FILE_PATTERNS          = {filePatterns}
                EXCLUDE_PATTERNS       = {excludePatterns}
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

    private static string Tail(string text)
    {
        var trimmed = text.Trim();
        const int maxLength = 500;
        return trimmed.Length <= maxLength ? trimmed : "..." + trimmed[^maxLength..];
    }
}
