using System.Globalization;
using System.Text.RegularExpressions;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;
using CSharpCodeAnalyst.Analyzers.Resources;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

public static class RuleParser
{
    // Allowed character for identifiers. Add - to end.
    private const string AllowedNameChars = @"[a-zA-Z0-9_-]+";

    // A single path segment. The optional leading dot allows compiler-generated member names
    // like the constructor ".ctor" or the static constructor ".cctor", which produce a double
    // dot in the full path (e.g. "MyClass..ctor" = "MyClass" + separator + ".ctor").
    private static readonly string NameSegment = $@"\.?{AllowedNameChars}";

    private static readonly string QualifiedName = $@"{NameSegment}(?:\.{NameSegment})*(?:\.\*{{1,2}})?";

    private static readonly Regex DenyRegex = new(
        $@"^\s*DENY\s*:\s*({QualifiedName})\s*->\s*({QualifiedName})\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RestrictRegex = new(
        $@"^\s*RESTRICT\s*:\s*({QualifiedName})\s*->\s*({QualifiedName})\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IsolateRegex = new(
        $@"^\s*ISOLATE\s*:\s*({QualifiedName})\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AllowRegex = new(
        $@"^\s*ALLOW\s*:\s*({QualifiedName})\s*->\s*({QualifiedName})\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // All metric rules share the shape "KEYWORD = value", with an optional pattern that scopes the
    // rule ("KEYWORD: Pattern = value"). One regex plus the factory registry below is enough - a new
    // metric rule costs a rule class and one entry, no parser change. The threshold is always written
    // with a dot as decimal separator, independent of the current culture, so a rules file stays portable.
    private static readonly Regex MetricRegex = new(
        $@"^\s*([a-zA-Z]+)\s*(?::\s*({QualifiedName})\s*)?=\s*(\d+(?:\.\d+)?)\s*$",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, Func<MetricRule>> MetricRuleFactories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [MaxCyclicityRule.RuleKeyword] = () => new MaxCyclicityRule(),
            [MaxLinesRule.RuleKeyword] = () => new MaxLinesRule()
        };

    public static RuleBase ParseRule(string ruleText)
    {
        if (string.IsNullOrWhiteSpace(ruleText))
        {
            throw new ArgumentException(Strings.Analyzer_Empty_Rule, nameof(ruleText));
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
                Target = denyMatch.Groups[2].Value.Trim()
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
                Target = restrictMatch.Groups[2].Value.Trim()
            };
        }

        // Try ISOLATE rule
        var isolateMatch = IsolateRegex.Match(trimmedRule);
        if (isolateMatch.Success)
        {
            return new IsolateRule
            {
                RuleText = trimmedRule,
                Source = isolateMatch.Groups[1].Value.Trim()
            };
        }

        // Try ALLOW rule (exception to DENY / RESTRICT / ISOLATE)
        var allowMatch = AllowRegex.Match(trimmedRule);
        if (allowMatch.Success)
        {
            return new AllowRule
            {
                RuleText = trimmedRule,
                Source = allowMatch.Groups[1].Value.Trim(),
                Target = allowMatch.Groups[2].Value.Trim()
            };
        }

        // Try metric rules
        var metricMatch = MetricRegex.Match(trimmedRule);
        if (metricMatch.Success)
        {
            return ParseMetricRule(trimmedRule, metricMatch.Groups[1].Value,
                metricMatch.Groups[2].Value.Trim(), metricMatch.Groups[3].Value);
        }

        throw new FormatException($"Invalid rule syntax: '{ruleText}'. Expected formats:\n" +
                                  "DENY: Source -> Target\n" +
                                  "RESTRICT: Source -> Target\n" +
                                  "ISOLATE: Source\n" +
                                  "ALLOW: Source -> Target\n" +
                                  $"{MaxCyclicityRule.RuleKeyword} = 15\n" +
                                  $"{MaxLinesRule.RuleKeyword}: Source = 50");
    }

    private static MetricRule ParseMetricRule(string ruleText, string keyword, string pattern, string value)
    {
        if (!MetricRuleFactories.TryGetValue(keyword, out var createRule))
        {
            var known = string.Join(", ", MetricRuleFactories.Keys);
            throw new FormatException($"Unknown metric rule '{keyword}'. Known metric rules: {known}.");
        }

        var rule = createRule();
        var threshold = double.Parse(value, CultureInfo.InvariantCulture);

        if (threshold < rule.MinThreshold || threshold > rule.MaxThreshold)
        {
            throw new FormatException($"Invalid threshold '{value}' for {rule.Keyword}. " +
                                      $"Expected a value between {rule.MinThreshold} and {rule.MaxThreshold}.");
        }

        switch (rule)
        {
            case CodeElementMetricRule elementRule:
                // An empty pattern scopes the rule to the whole graph.
                elementRule.Source = pattern;
                break;
            case SystemMetricRule when pattern.Length > 0:
                throw new FormatException($"{rule.Keyword} applies to the whole system and cannot be scoped by the pattern '{pattern}'.");
        }

        rule.RuleText = ruleText;
        rule.Threshold = threshold;
        return rule;
    }

    public static List<RuleBase> ParseRules(string rulesText)
    {
        var rules = new List<RuleBase>();

        if (string.IsNullOrWhiteSpace(rulesText))
            return rules;

        var lines = rulesText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < lines.Length; i++)
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