using System.Text.RegularExpressions;

namespace CodeParser.Parser.Config;

public class ParserConfig
{
    private ProjectExclusionRegExCollection _projectExclusionFilters;

    public ParserConfig(ProjectExclusionRegExCollection projectExclusionFilters)
    {
        _projectExclusionFilters = projectExclusionFilters;
    }

    public bool IsProjectIncluded(string projectName)
    {
        return _projectExclusionFilters.IsProjectIncluded(projectName);
    }
}