namespace CodeParser.Parser.Config;

public class ParserConfig
{
    private readonly ProjectExclusionRegExCollection _projectExclusionFilters;

    public ParserConfig(ProjectExclusionRegExCollection projectExclusionFilters, bool includeExternals,
        bool includeGeneratedCode = false)
    {
        _projectExclusionFilters = projectExclusionFilters;
        IncludeExternals = includeExternals;
        IncludeGeneratedCode = includeGeneratedCode;
    }

    public bool IncludeExternals { get; }

    /// <summary>
    ///     When enabled, source-generated documents (e.g. CommunityToolkit.Mvvm [ObservableProperty] /
    ///     [RelayCommand], [GeneratedRegex], ...) are included in phase 1 so the generated members get
    ///     their own code element instead of being collapsed onto the containing type via the phase-2
    ///     fallback.
    /// </summary>
    public bool IncludeGeneratedCode { get; }

    public bool IsProjectIncluded(string projectName)
    {

        return _projectExclusionFilters.IsProjectIncluded(projectName);
    }
}