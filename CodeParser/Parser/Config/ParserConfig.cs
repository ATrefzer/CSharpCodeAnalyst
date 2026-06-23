namespace CodeParser.Parser.Config;

public class ParserConfig
{
    private readonly ProjectExclusionRegExCollection _projectExclusionFilters;

    public ParserConfig(ProjectExclusionRegExCollection projectExclusionFilters, bool includeExternals,
        bool includeGeneratedCode = false, bool splitPropertyAccessors = false)
    {
        _projectExclusionFilters = projectExclusionFilters;
        IncludeExternals = includeExternals;
        IncludeGeneratedCode = includeGeneratedCode;
        SplitPropertyAccessors = splitPropertyAccessors;
    }

    public bool IncludeExternals { get; }

    /// <summary>
    ///     When enabled, source-generated documents (e.g. CommunityToolkit.Mvvm [ObservableProperty] /
    ///     [RelayCommand], [GeneratedRegex], ...) are included in phase 1 so the generated members get
    ///     their own code element instead of being collapsed onto the containing type via the phase-2
    ///     fallback.
    /// </summary>
    public bool IncludeGeneratedCode { get; }

    /// <summary>
    ///     When enabled, each property is split into its getter and setter accessor as separate child
    ///     elements (e.g. <c>get_Prop</c> / <c>set_Prop</c>). This lets the dependency graph distinguish
    ///     read access from write access and avoids false cycles that arise when both directions are
    ///     merged onto a single property node.
    /// </summary>
    public bool SplitPropertyAccessors { get; }

    public bool IsProjectIncluded(string projectName)
    {

        return _projectExclusionFilters.IsProjectIncluded(projectName);
    }
}