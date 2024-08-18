using System.Text.RegularExpressions;

namespace CodeParser.Parser.Config
{
    public class ProjectExclusionRegExCollection
    {
        public List<string> Expressions { get; private set; } = [];

        public void Initialize(List<string> expressions)
        {
            Expressions = expressions;
        }
        public void Initialize(string expressions, string separator)
        {
            List<string> separators = [separator];

            Expressions = expressions.Split(separators.ToArray(), StringSplitOptions.RemoveEmptyEntries)
           .Select(f => f.Trim())
           .Where(f => !string.IsNullOrWhiteSpace(f))
           .ToList();
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
}
