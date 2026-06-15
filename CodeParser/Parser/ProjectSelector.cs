using System.Text.RegularExpressions;

namespace CodeParser.Parser;

/// <summary>
///     A project reduced to the bits needed to decide which one to keep when several projects share the
///     same assembly name. Kept free of Roslyn types so the selection logic can be unit-tested directly.
/// </summary>
internal record ProjectCandidate(string AssemblyName, string? FilePath, string ProjectName);

/// <summary>
///     Result of <see cref="ProjectSelector.SelectProjectsPerAssembly" />.
/// </summary>
internal record ProjectSelectionResult(
    List<ProjectCandidate> Selected,
    List<string> Warnings,
    List<string> Failures);

/// <summary>
///     We can only keep one project per assembly name (the symbol key is built from the assembly name, so
///     two assemblies with the same name collide). Two situations lead here:
///     <list type="number">
///         <item>
///             Multi-targeting: the same .csproj is opened once per target framework (same file path,
///             different TFM suffix in the name). Keeping exactly one is correct; we keep the highest TFM.
///         </item>
///         <item>
///             A real name collision: different .csproj files that happen to produce the same assembly
///             name. Keeping one means losing the others - reported as a failure, not a harmless warning.
///         </item>
///     </list>
/// </summary>
internal static class ProjectSelector
{
    private static readonly Regex TfmSuffix = new(@"\(([^)]+)\)\s*$", RegexOptions.Compiled);

    public static ProjectSelectionResult SelectProjectsPerAssembly(IReadOnlyList<ProjectCandidate> candidates)
    {
        var selected = new List<ProjectCandidate>();
        var warnings = new List<string>();
        var failures = new List<string>();

        // Group by assembly name but keep a deterministic order so the chosen project is reproducible.
        var groups = candidates
            .Select((candidate, index) => (candidate, index))
            .GroupBy(x => x.candidate.AssemblyName)
            .OrderBy(g => g.Min(x => x.index));

        foreach (var group in groups)
        {
            var members = group.Select(x => x.candidate).ToList();
            if (members.Count == 1)
            {
                selected.Add(members[0]);
                continue;
            }

            // Highest TFM wins; ties (or unparsable TFMs) fall back to a stable name order.
            var chosen = members
                .OrderByDescending(GetTfmRank)
                .ThenBy(m => m.ProjectName, StringComparer.Ordinal) // Tie-breaker for same TFM or unparsable TFMs, ensures deterministic selection.
                .First();
            selected.Add(chosen);

            if (IsMultiTargeting(members))
            {
                var tfms = string.Join(", ", members.Select(GetTfm).OrderBy(t => t, StringComparer.Ordinal));
                warnings.Add(
                    $"Project '{chosen.AssemblyName}' is multi-targeted ({tfms}); analyzing '{GetTfm(chosen)}' only.");
            }
            else
            {
                var paths = string.Join(", ", members.Select(m => m.FilePath ?? m.ProjectName));
                failures.Add(
                    $"Multiple projects produce assembly '{chosen.AssemblyName}' ({paths}); " +
                    $"only '{chosen.FilePath ?? chosen.ProjectName}' is analyzed, the others are ignored.");
            }
        }

        return new ProjectSelectionResult(selected, warnings, failures);
    }

    /// <summary>
    ///     Multi-targeting means the very same project file appears more than once. If any file path is
    ///     missing we cannot prove that, so we treat it as a real collision (the louder diagnostic).
    /// </summary>
    private static bool IsMultiTargeting(IReadOnlyList<ProjectCandidate> members)
    {
        var first = members[0].FilePath;
        return !string.IsNullOrEmpty(first) &&
               members.All(m => string.Equals(m.FilePath, first, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Tfm = Target Framework Moniker. When multitarget project it is part of the project name
    ///     TargetFramework => project xyz (net10.0)
    /// </summary>
    private static string GetTfm(ProjectCandidate candidate)
    {
        var match = TfmSuffix.Match(candidate.ProjectName);
        return match.Success ? match.Groups[1].Value.Trim() : candidate.ProjectName;
    }

    /// <summary>
    ///     Maps a target framework to a comparable rank so that, for example, net10.0 outranks net8.0 and
    ///     both outrank netstandard2.0 / net48. Unrecognized monikers rank lowest
    /// </summary>
    private static (int family, Version version) GetTfmRank(ProjectCandidate candidate)
    {
        var tfm = GetTfm(candidate).ToLowerInvariant();

        // .NET 5+ (net5.0, net8.0, net10.0, optionally with an OS suffix like net8.0-windows).
        var modern = Regex.Match(tfm, @"^net(\d+)\.(\d+)");
        if (modern.Success)
        {
            return (4, new Version(int.Parse(modern.Groups[1].Value), int.Parse(modern.Groups[2].Value)));
        }

        // .NET Core (netcoreapp3.1).
        var core = Regex.Match(tfm, @"^netcoreapp(\d+)\.(\d+)");
        if (core.Success)
        {
            return (3, new Version(int.Parse(core.Groups[1].Value), int.Parse(core.Groups[2].Value)));
        }

        // .NET Standard (netstandard2.0) - the portable contract, preferred over old .NET Framework.
        var standard = Regex.Match(tfm, @"^netstandard(\d+)\.(\d+)");
        if (standard.Success)
        {
            return (2, new Version(int.Parse(standard.Groups[1].Value), int.Parse(standard.Groups[2].Value)));
        }

        // .NET Framework (net48, net472).
        var framework = Regex.Match(tfm, @"^net(\d)(\d)(\d?)$");
        if (framework.Success)
        {
            var minor = framework.Groups[2].Value + framework.Groups[3].Value;
            return (1, new Version(int.Parse(framework.Groups[1].Value), int.Parse(minor)));
        }

        return (0, new Version(0, 0));
    }
}