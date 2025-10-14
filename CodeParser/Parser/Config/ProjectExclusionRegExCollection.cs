using System.Text.RegularExpressions;

namespace CodeParser.Parser.Config;

public class ProjectExclusionRegExCollection
{
    public List<string> Expressions { get; private set; } = [];

    private static void ThrowIfInvalidRegex(List<string> expressions)
    {
        foreach (var expression in expressions)
        {
            // Throw exception if not valid
            _ = Regex.Match("", expression);
        }
    }


    public void Initialize(string filterText)
    {
        // Accept various inputs.
        char[] separators = [';', '\r', '\n'];

        var expressions =filterText
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .ToList();
        
        ThrowIfInvalidRegex(expressions);
        Expressions = expressions;
    }


    public bool IsProjectIncluded(string projectName)
    {
        foreach (var regEx in Expressions)
        {
            if (Regex.IsMatch(projectName, regEx))
            {
                return false;
            }
        }

        // No filter applied
        return true;
    }

    public override string ToString()
    {
        return string.Join(";", Expressions);
    }
}