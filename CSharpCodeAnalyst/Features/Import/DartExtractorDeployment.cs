using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CSharpCodeAnalyst.Features.Import;

/// <summary>
///     Makes the DartExtractor tool runnable.
///     The tool ships as Dart sources next to the executable, and running it needs a
///     "dart pub get" first - which writes .dart_tool/ and pubspec.lock into the package
///     directory. The application directory may well be read-only (Program Files), so the
///     sources are copied to %LocalAppData% and resolved there, once per source version.
///     The version is a fingerprint over the shipped files rather than the application
///     version: during development the sources change while the version does not, and a
///     stale resolved copy would be silently used forever.
/// </summary>
internal static class DartExtractorDeployment
{
    private const string ReadyMarker = ".pub-get-done";

    /// <summary>
    ///     Returns the directory of the ready-to-run extractor package. Cheap after the first
    ///     call - it only recomputes the fingerprint and finds the marker file.
    /// </summary>
    public static async Task<string> EnsureDeployedAsync(string dartExecutable, IProgress<string>? progress, CancellationToken cancellationToken = default)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "DartExtractor");
        if (!File.Exists(Path.Combine(source, "pubspec.yaml")))
        {
            throw new InvalidOperationException($"The DartExtractor tool is missing from the installation ({source}).");
        }

        var target = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CSharpCodeAnalyst", "DartExtractor", ComputeFingerprint(source));

        if (File.Exists(Path.Combine(target, ReadyMarker)))
        {
            return target;
        }

        progress?.Report(Resources.Strings.ImportDart_PreparingTool);

        // A previous attempt may have failed halfway through; start from a clean copy.
        if (Directory.Exists(target))
        {
            Directory.Delete(target, true);
        }

        CopyDirectory(source, target);
        await RunPubGetAsync(dartExecutable, target, cancellationToken);

        await File.WriteAllTextAsync(Path.Combine(target, ReadyMarker), DateTime.UtcNow.ToString("O"), cancellationToken);
        return target;
    }

    private static async Task RunPubGetAsync(string dartExecutable, string packageDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessRunner.Options(dartExecutable, ["pub", "get"], packageDirectory);
        var result = await ProcessRunner.RunAsync(startInfo, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'dart pub get' failed for the DartExtractor tool (exit code {result.ExitCode}). {result.ErrorTail}");
        }
    }

    /// <summary>
    ///     Hash over relative path, size and write time of every shipped file. Enough to notice
    ///     a changed or added source file; not a security boundary.
    /// </summary>
    private static string ComputeFingerprint(string directory)
    {
        var builder = new StringBuilder();
        foreach (var file in EnumerateSourceFiles(directory).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var info = new FileInfo(file);
            builder.Append(Path.GetRelativePath(directory, file))
                .Append('|').Append(info.Length)
                .Append('|').Append(info.LastWriteTimeUtc.Ticks)
                .Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return System.Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static IEnumerable<string> EnumerateSourceFiles(string directory)
    {
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => !IsGenerated(Path.GetRelativePath(directory, f)));
    }

    /// <summary>
    ///     .dart_tool holds absolute paths of the machine that resolved the package, so it must
    ///     neither be copied nor influence the fingerprint.
    /// </summary>
    private static bool IsGenerated(string relativePath)
    {
        return relativePath.StartsWith(".dart_tool", StringComparison.OrdinalIgnoreCase) ||
               relativePath.Equals(ReadyMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyDirectory(string source, string target)
    {
        foreach (var file in EnumerateSourceFiles(source))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }
}
