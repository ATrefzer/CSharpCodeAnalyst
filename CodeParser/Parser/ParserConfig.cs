using System.Text.RegularExpressions;

namespace CodeParser.Parser;

public class ParserConfig(List<string> projectExcludeRegEx)
{
    public bool IsProjectIncluded(string projectName)
    {
        foreach (var regEx in projectExcludeRegEx)
        {
            if (Regex.IsMatch(projectName, regEx))
            {
                return false;
            }
        }

        // No filter applied
        return true;
    }
}