using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;

namespace CSharpCodeAnalyst.CodeParser.Xaml;

/// <summary>
///     Answers which XAML files belong to a project.
///     <para>
///         Roslyn cannot tell us: a <c>Microsoft.CodeAnalysis.Project</c> exposes Documents,
///         AdditionalDocuments and AnalyzerConfigDocuments, and a <c>Page</c> is none of them. The project
///         file is therefore evaluated a second time, with the MSBuild engine
///         <c>Initializer.InitializeMsBuildLocator</c> has already put in place. That gives the item list
///         the build itself works with - in particular a file taken out again by
///         <c>&lt;Page Remove="..." /&gt;</c> appears in no item group at all and is correctly gone.
///     </para>
///     <para>
///         Scanning the directory - what this did before - reads whatever happens to lie there: a file
///         excluded from the project, a leftover from another branch, or the XAML of a project nested
///         inside this one. It remains the fallback for a project file that cannot be evaluated.
///     </para>
///     <para>
///         A linked file (<c>&lt;Page Include="..\Shared\Foo.xaml"&gt;</c>) comes along for free, because
///         the item carries its real path. That is a side effect, not the goal - the directory scan misses
///         those and always did.
///     </para>
///     <para>
///         The evaluation costs a few hundred milliseconds per project (the first one more, it warms up the
///         engine). Every project shares one <see cref="ProjectCollection" /> so the SDK imports are
///         evaluated once, which is why this is an instance and not a static helper.
///     </para>
/// </summary>
public sealed class XamlFileLocator : IDisposable
{
    /// <summary>
    ///     The item types a XAML file can legitimately have. Everything else is either not XAML we could
    ///     read or not ours: the SDK contributes twenty <c>PropertyPageSchema</c> items pointing into the
    ///     dotnet installation, which a plain "all evaluated items ending in .xaml" would pick up.
    /// </summary>
    private static readonly HashSet<string> XamlItemTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Compiled to BAML by the WPF markup compiler.
        "ApplicationDefinition", "Page",

        // Embedded or copied and loaded at runtime (loose XAML, themes).
        "Resource", "Content",

        // What the SDK default glob puts a stray XAML file into when the project does not compile it.
        "None"
    };

    /// <summary>
    ///     The <see cref="ProjectCollection" />, created on first use and typed as <see cref="object" /> on
    ///     purpose: the field's type appears in <see cref="Dispose" />, and touching an MSBuild type there
    ///     would load the assembly even for a run that never evaluates a project - an in-memory parse has
    ///     no MSBuild at all.
    /// </summary>
    private object? _collection;

    public void Dispose()
    {
        (_collection as IDisposable)?.Dispose();
    }

    /// <param name="projectFilePath">
    ///     The project file. Null for a project Roslyn produced without one, which leaves only the scan.
    /// </param>
    /// <param name="projectDirectory">The directory used by the fallback scan.</param>
    public IReadOnlyList<string> Locate(string? projectFilePath, string projectDirectory)
    {
        // Two ways to have nothing to evaluate, both of them normal rather than a defect: an in-memory
        // parse (ParseSourceAsync) builds its project around the synthetic path "InMemory.csproj" and
        // never registers a locator, and without a registered locator there is no MSBuild at all.
        if (projectFilePath is not null && File.Exists(projectFilePath) && MSBuildLocator.IsRegistered)
        {
            try
            {
                return FromProjectFile(projectFilePath);
            }
            catch (Exception exception)
            {
                // A project we cannot evaluate must not cost us its references - and it must not break the
                // parse run either. Broad on purpose: MSBuild throws its own exception type for an invalid
                // project, IO exceptions for a file that moved, the SDK resolvers can fail on their own,
                // and preparing FromProjectFile is where a missing MSBuild assembly would surface.
                Trace.TraceWarning(
                    $"XAML: cannot evaluate '{projectFilePath}', falling back to a directory scan. {exception.Message}");
            }
        }

        return EnumerateDirectory(projectDirectory);
    }

    /// <summary>
    ///     An empty result is an answer, not a failure: a project without XAML items has no XAML, and
    ///     falling back to the scan here would bring the excluded files straight back in.
    /// </summary>
    private IReadOnlyList<string> FromProjectFile(string projectFilePath)
    {
        // Evaluated without global properties. The XAML item groups are not written per configuration in
        // any project we have seen, and guessing the configuration Roslyn used would be worse than not
        // setting one.
        var collection = (ProjectCollection)(_collection ??= new ProjectCollection());
        var project = collection.LoadProject(projectFilePath);

        return project.AllEvaluatedItems
            .Where(item => XamlItemTypes.Contains(item.ItemType))
            .Select(item => item.GetMetadataValue("FullPath"))
            .Where(IsXamlFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    ///     The fallback: every XAML file below the directory. The output directories are skipped - the
    ///     markup compiler copies XAML into <c>obj</c>, and everything found there is a duplicate.
    /// </summary>
    public static IReadOnlyList<string> EnumerateDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*.xaml", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
    }

    /// <summary>
    ///     An item type from the list above can hold anything (a <c>Content</c> item is usually not XAML),
    ///     and an item can name a file that is not on disk.
    /// </summary>
    private static bool IsXamlFile(string path)
    {
        return path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) && File.Exists(path);
    }
}
