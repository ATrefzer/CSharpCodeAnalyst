using System.Diagnostics;
using Microsoft.Build.Locator;

namespace CSharpCodeAnalyst.CodeParser.Parser;

public static class Initializer
{
    public static void InitializeMsBuildLocator()
    {
        // Without the MSBuildLocator the Project.Documents list is empty!
        // Referencing MSBuild packages directly and copy to output is not reliable and causes
        // hard to find problems

        try
        {
            MSBuildLocator.RegisterDefaults();
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);
            RegisterMsBuildManually();
        }
    }

    private static void RegisterMsBuildManually()
    {
        var fallback = GetFallbackMsBuildPath();
        if (string.IsNullOrEmpty(fallback))
        {
            throw new InvalidOperationException(
                "Failed to register MSBuild path. Please ensure that MSBuild is installed and the path is correct.");
        }

        Trace.WriteLine("Registering MSBuild manually: " + fallback);
        MSBuildLocator.RegisterMSBuildPath(fallback);
    }

    /// <summary>
    ///     Returns the first candidate directory that actually contains MSBuild, or an empty string.
    /// </summary>
    private static string GetFallbackMsBuildPath()
    {
        return EnumerateMsBuildCandidates().FirstOrDefault(ContainsMsBuild) ?? string.Empty;
    }

    private static bool ContainsMsBuild(string directory)
    {
        // SDK installations ship MSBuild.dll, Visual Studio installations ship MSBuild.exe.
        return File.Exists(Path.Combine(directory, "MSBuild.dll")) ||
               File.Exists(Path.Combine(directory, "MSBuild.exe"));
    }

    /// <summary>
    ///     Known MSBuild locations, best match first. Only used when
    ///     <see cref="MSBuildLocator.RegisterDefaults" /> failed.
    /// </summary>
    private static IEnumerable<string> EnumerateMsBuildCandidates()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // 1. .NET SDK. We run on .NET ourselves, so the SDK's MSBuild is the best match. This is what
        //    RegisterDefaults would have chosen - trying it again covers failures like a broken PATH,
        //    where the SDK directory still exists.
        var sdkRoot = Path.Combine(programFiles, "dotnet", "sdk");
        foreach (var sdkDirectory in EnumerateVersionDirectoriesDescending(sdkRoot))
        {
            yield return sdkDirectory;
        }

        // 2. Visual Studio, newest version first. VS 2026 installs under the major version
        //    ("18"); "BuildTools" installs under Program Files (x86) even for the 64-bit studios, hence both roots.
        string[] vsVersions = ["18", "2022"];
        string[] vsEditions = ["Enterprise", "Professional", "Community", "BuildTools"];
        string[] vsRoots =
        [
            Path.Combine(programFiles, "Microsoft Visual Studio"),
            Path.Combine(programFilesX86, "Microsoft Visual Studio")
        ];

        foreach (var version in vsVersions)
        {
            foreach (var root in vsRoots)
            {
                foreach (var edition in vsEditions)
                {
                    yield return Path.Combine(root, version, edition, "MSBuild", "Current", "Bin");
                }
            }
        }

        // 3. JetBrains Rider bundles MSBuild under tools\MSBuild\Current\Bin. The install folder name
        //    carries the version ("JetBrains Rider 2024.3") for the standalone installer, while the
        //    Toolbox app installs to a stable "Rider" folder under %LocalAppData%\Programs.
        string[] riderRoots =
        [
            Path.Combine(programFiles, "JetBrains"),
            Path.Combine(localAppData, "Programs")
        ];

        foreach (var root in riderRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var riderDirectories = Directory.EnumerateDirectories(root, "*Rider*")
                .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase);
            foreach (var riderDirectory in riderDirectories)
            {
                yield return Path.Combine(riderDirectory, "tools", "MSBuild", "Current", "Bin");
            }
        }
    }

    /// <summary>
    ///     Subdirectories of <paramref name="root" /> whose names parse as a version, newest first.
    ///     A pre-release suffix ("10.0.100-preview.5") is ignored for the comparison.
    /// </summary>
    private static IEnumerable<string> EnumerateVersionDirectoriesDescending(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory.EnumerateDirectories(root)
            .Select(directory =>
            {
                var name = Path.GetFileName(directory);
                var dash = name.IndexOf('-');
                var isVersion = Version.TryParse(dash >= 0 ? name[..dash] : name, out var version);
                return (Directory: directory, IsVersion: isVersion, Version: version, IsPreview: dash >= 0);
            })
            .Where(x => x.IsVersion)
            .OrderByDescending(x => x.Version)
            .ThenBy(x => x.IsPreview) // Same version: prefer the release over the preview.
            .Select(x => x.Directory);
    }
}
