using System.Diagnostics;
using Microsoft.Build.Locator;

namespace CodeParser.Parser;

public class Initializer
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
        catch(Exception ex)
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

        MSBuildLocator.RegisterMSBuildPath(fallback);
    }

    private static string GetFallbackMsBuildPath()
    {
        var fallbacks = new[] { @"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin" };
        foreach (var fallback in fallbacks)
        {
            if (Directory.Exists(fallback))
            {
                return fallback;
            }
        }

        return string.Empty;
    }
}