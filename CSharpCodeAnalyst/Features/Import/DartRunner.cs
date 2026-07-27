using System.IO;

namespace CSharpCodeAnalyst.Features.Import;

/// <summary>
///     Locates the Dart SDK and runs the DartExtractor tool over a Dart or Flutter project.
/// </summary>
internal static class DartRunner
{
    /// <summary>
    ///     Path to dart.exe, or null when no Dart SDK can be found.
    ///     Note that "dart" on PATH is usually Flutter's dart.bat wrapper, which cannot be started
    ///     without a shell. So the real dart.exe inside the Flutter SDK is preferred; a standalone
    ///     Dart SDK ships dart.exe on PATH directly.
    /// </summary>
    public static string? FindDartExecutable()
    {
        foreach (var directory in GetPathDirectories())
        {
            var executable = Path.Combine(directory, "dart.exe");
            if (File.Exists(executable))
            {
                return executable;
            }

            // <flutterRoot>\bin holds dart.bat and flutter.bat; the SDK it manages sits below it.
            var flutterDartSdk = Path.Combine(directory, "cache", "dart-sdk", "bin", "dart.exe");
            if ((File.Exists(Path.Combine(directory, "dart.bat")) || File.Exists(Path.Combine(directory, "flutter.bat")))
                && File.Exists(flutterDartSdk))
            {
                return flutterDartSdk;
            }
        }

        return null;
    }

    /// <summary>
    ///     A project must have been resolved ("flutter pub get") before it can be analyzed,
    ///     otherwise package: URIs do not resolve and the graph stays nearly empty. In a pub
    ///     workspace the package config lives at the workspace root, so parent directories count.
    /// </summary>
    public static bool IsProjectResolved(string projectDirectory)
    {
        var current = new DirectoryInfo(projectDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, ".dart_tool", "package_config.json")))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    /// <summary>
    ///     Runs the extractor and returns the path of the written JSON file.
    /// </summary>
    public static async Task<string> RunAsync(string projectDirectory, string workingDirectory, IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        var dartExecutable = FindDartExecutable()
                             ?? throw new InvalidOperationException(Resources.Strings.ImportDart_DartNotFound);

        var extractorDirectory = await DartExtractorDeployment.EnsureDeployedAsync(dartExecutable, progress, cancellationToken);

        Directory.CreateDirectory(workingDirectory);
        var outputPath = Path.Combine(workingDirectory, "graph.json");

        progress?.Report(Resources.Strings.ImportDart_RunningExtractor);

        var options = new ProcessRunner.Options(
            dartExecutable,
            ["run", Path.Combine("bin", "extract.dart"), projectDirectory, outputPath],
            extractorDirectory);

        var result = await ProcessRunner.RunAsync(options, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"The Dart extractor exited with code {result.ExitCode}. {result.ErrorTail}");
        }

        if (!File.Exists(outputPath))
        {
            throw new InvalidOperationException($"The Dart extractor finished but wrote no output. {result.ErrorTail}");
        }

        return outputPath;
    }

    private static IEnumerable<string> GetPathDirectories()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            yield break;
        }

        foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // A malformed PATH entry must not take the whole lookup down.
            string? directory = null;
            try
            {
                directory = Path.GetFullPath(entry.Trim('"'));
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Ignore and continue with the next entry.
            }

            if (directory is not null)
            {
                yield return directory;
            }
        }
    }
}
