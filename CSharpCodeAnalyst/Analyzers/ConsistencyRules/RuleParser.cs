using System.Text.RegularExpressions;
using CSharpCodeAnalyst.Analyzers.ConsistencyRules.Rules;

namespace CSharpCodeAnalyst.Analyzers.ConsistencyRules;

public static class RuleParser
{
    private static readonly Regex DenyRegex = new(@"^\s*DENY\s*:\s*(.+?)\s*->\s*(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RestrictRegex = new(@"^\s*RESTRICT\s*:\s*(.+?)\s*->\s*(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IsolateRegex = new(@"^\s*ISOLATE\s*:\s*(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ConsistencyRuleBase ParseRule(string ruleText)
    {
        if (string.IsNullOrWhiteSpace(ruleText))
        {
            throw new ArgumentException("Rule text cannot be empty", nameof(ruleText));
        }

        var trimmedRule = ruleText.Trim();

        // Try DENY rule
        var denyMatch = DenyRegex.Match(trimmedRule);
        if (denyMatch.Success)
        {
            return new DenyRule
            {
                RuleText = trimmedRule,
                Source = denyMatch.Groups[1].Value.Trim(),
                Target = denyMatch.Groups[2].Value.Trim(),
                Description = $"Deny dependencies from {denyMatch.Groups[1].Value.Trim()} to {denyMatch.Groups[2].Value.Trim()}"
            };
        }

        // Try RESTRICT rule
        var restrictMatch = RestrictRegex.Match(trimmedRule);
        if (restrictMatch.Success)
        {
            return new RestrictRule
            {
                RuleText = trimmedRule,
                Source = restrictMatch.Groups[1].Value.Trim(),
                Target = restrictMatch.Groups[2].Value.Trim(),
                Description = $"Restrict {restrictMatch.Groups[1].Value.Trim()} to only depend on {restrictMatch.Groups[2].Value.Trim()}"
            };
        }

        // Try ISOLATE rule
        var isolateMatch = IsolateRegex.Match(trimmedRule);
        if (isolateMatch.Success)
        {
            return new IsolateRule
            {
                RuleText = trimmedRule,
                Source = isolateMatch.Groups[1].Value.Trim(),
                Description = $"Isolate {isolateMatch.Groups[1].Value.Trim()} from external dependencies"
            };
        }

        throw new FormatException($"Invalid rule syntax: '{ruleText}'. Expected formats:\n" +
                                  "DENY: Source -> Target\n" +
                                  "RESTRICT: Source -> Target\n" +
                                  "ISOLATE: Source");
    }

    public static List<ConsistencyRuleBase> ParseRules(string rulesText)
    {
        var rules = new List<ConsistencyRuleBase>();

        if (string.IsNullOrWhiteSpace(rulesText))
            return rules;

        var lines = rulesText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Skip comments and empty lines
            if (string.IsNullOrEmpty(line) || line.StartsWith("//"))
                continue;

            try
            {
                var rule = ParseRule(line);
                rules.Add(rule);
            }
            catch (Exception ex)
            {
                throw new FormatException($"Error parsing rule on line {i + 1}: {ex.Message}", ex);
            }
        }

        return rules;
    }
}