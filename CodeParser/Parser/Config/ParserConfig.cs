namespace CodeParser.Parser.Config;

public class ParserConfig
{
    private readonly ProjectExclusionRegExCollection _projectExclusionFilters;
    public bool IncludeExternals { get; }

    public ParserConfig(ProjectExclusionRegExCollection projectExclusionFilters, bool includeExternals)
    {
        _projectExclusionFilters = projectExclusionFilters;
        IncludeExternals = includeExternals;
    }

    public bool IsProjectIncluded(string projectName)
    {
        return _projectExclusionFilters.IsProjectIncluded(projectName);
    }
}