using System.Text;
using System.Text.RegularExpressions;

namespace CSharpCodeAnalyst.Common;

public static class PascalCaseSearch
{
    public static (bool isPascalCase, Regex? regex) CreateSearchRegex(string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
        {
            return (false, null);
        }

        // Check if search term contains both upper and lower case
        var hasUpper = searchTerm.Any(char.IsUpper);
        //var hasLower = searchTerm.Any(char.IsLower);
        //var isPascalCase = hasUpper && hasLower;
        var isPascalCase = hasUpper;

        if (!isPascalCase)
        {
            return (false, null);
        }

        // Build PascalCase regex pattern
        var pattern = new StringBuilder();
        var segments = new List<string>();
        var currentSegment = "";

        foreach (var c in searchTerm)
        {
            if (char.IsUpper(c))
            {
                if (currentSegment.Length > 0)
                {
                    segments.Add(currentSegment);
                }
                currentSegment = c.ToString();
            }
            else
            {
                currentSegment += c;
            }
        }

        if (currentSegment.Length > 0)
        {
            segments.Add(currentSegment);
        }

        // Join segments with [a-z0-9]* wildcard
        pattern.Append(string.Join("[a-z0-9]*", segments.Select(s => Regex.Escape(s))));

        var regex = new Regex(pattern.ToString(), RegexOptions.Compiled);
        return (true, regex);
    }
}