namespace CodeParser.Parser.Config;

public class ParserConfig
{
    private readonly ProjectExclusionRegExCollection _projectExclusionFilters;

    public ParserConfig(ProjectExclusionRegExCollection projectExclusionFilters, int maxDegreeOfParallelism)
    {
        _projectExclusionFilters = projectExclusionFilters;
        MaxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    public int MaxDegreeOfParallelism { get; set; }

    public bool IsProjectIncluded(string projectName)
    {
        return _projectExclusionFilters.IsProjectIncluded(projectName);
    }
}