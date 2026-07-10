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

    /// <summary>Tried in order. Group 1 of each regex is the source, group 2 the target if there is one.</summary>
    private static readonly (Regex Regex, Func<DependencyRule> CreateRule)[] DependencyRuleFactories =
    [
        (DenyRegex, () => new DenyRule()),
        (RestrictRegex, () => new RestrictRule()),
        (IsolateRegex, () => new IsolateRule()),
        (AllowRegex, () => new AllowRule())
    ];

    public static List<RuleBase> ParseRules(string rulesText)
    {
        var rules = new List<RuleBase>();

        if (string.IsNullOrWhiteSpace(rulesText))
        {
            return rules;
        }

        var lines = rulesText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Skip comments and empty lines
            if (string.IsNullOrEmpty(line) || line.StartsWith("//"))
            {
                continue;
            }

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

    public static RuleBase ParseRule(string ruleTextLine)
    {
        if (string.IsNullOrWhiteSpace(ruleTextLine))
        {
            throw new ArgumentException(Strings.Analyzer_Empty_Rule, nameof(ruleTextLine));
        }

        var trimmedRule = ruleTextLine.Trim();

        return ParseDependencyRule(trimmedRule)
               ?? (RuleBase?)ParseMetricRule(trimmedRule)
               ?? throw new FormatException($"Invalid rule syntax: '{ruleTextLine}'. Expected formats:\n" +
                                            "DENY: Source -> Target\n" +
                                            "RESTRICT: Source -> Target\n" +
                                            "ISOLATE: Source\n" +
                                            "ALLOW: Source -> Target\n" +
                                            $"{MaxCyclicityRule.RuleKeyword} = 15\n" +
                                            $"{MaxLinesRule.RuleKeyword}: Source = 50");
    }

    /// <summary>
    ///     All dependency rules have the shape "KEYWORD: Source [-> Target]". ISOLATE is the only one
    ///     without a target, and its regex therefore leaves the second group empty.
    /// </summary>
    private static DependencyRule? ParseDependencyRule(string ruleText)
    {
        foreach (var (regex, createRule) in DependencyRuleFactories)
        {
            var match = regex.Match(ruleText);
            if (!match.Success)
            {
                continue;
            }

            var rule = createRule();
            rule.RuleText = ruleText;
            rule.Source = match.Groups[1].Value.Trim();

            if (rule is TargetedDependencyRule ruleWithTarget)
            {
                ruleWithTarget.Target = match.Groups[2].Value.Trim();
            }

            return rule;
        }

        return null;
    }

    private static MetricRule? ParseMetricRule(string ruleText)
    {
        var match = MetricRegex.Match(ruleText);
        if (!match.Success)
        {
            return null;
        }

        return CreateMetricRule(ruleText, match.Groups[1].Value, match.Groups[2].Value.Trim(), match.Groups[3].Value);
    }

    private static MetricRule CreateMetricRule(string ruleText, string keyword, string pattern, string value)
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
}