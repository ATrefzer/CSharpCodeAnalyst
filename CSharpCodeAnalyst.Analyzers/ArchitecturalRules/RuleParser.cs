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

    // Metric rule. The threshold is always written with a dot as decimal separator, independent
    // of the current culture, so that a rules file stays portable.
    private static readonly Regex MaxCyclicityRegex = new(
        @"^\s*MAXCYCLICITY\s*[:=]\s*(\d+(?:\.\d+)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

        // Try ALLOW rule (exception to DENY / RESTRICT / ISOLATE)
        var allowMatch = AllowRegex.Match(trimmedRule);
        if (allowMatch.Success)
        {
            return new AllowRule
            {
                RuleText = trimmedRule,
                Source = allowMatch.Groups[1].Value.Trim(),
                Target = allowMatch.Groups[2].Value.Trim(),
                Description = $"Allow dependencies from {allowMatch.Groups[1].Value.Trim()} to {allowMatch.Groups[2].Value.Trim()} (exception)"
            };
        }

        // Try MAXCYCLICITY rule (system-wide metric, no pattern)
        var maxCyclicityMatch = MaxCyclicityRegex.Match(trimmedRule);
        if (maxCyclicityMatch.Success)
        {
            var threshold = double.Parse(maxCyclicityMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            if (threshold > 1.0)
            {
                throw new FormatException($"Invalid cyclicity threshold '{maxCyclicityMatch.Groups[1].Value}'. Expected a value between 0 and 1.");
            }

            return new MaxCyclicityRule
            {
                RuleText = trimmedRule,
                MaxCyclicity = threshold,
                Description = $"Cyclicity of the system must not exceed {threshold.ToString(CultureInfo.InvariantCulture)}"
            };
        }

        throw new FormatException($"Invalid rule syntax: '{ruleText}'. Expected formats:\n" +
                                  "DENY: Source -> Target\n" +
                                  "RESTRICT: Source -> Target\n" +
                                  "ISOLATE: Source\n" +
                                  "ALLOW: Source -> Target\n" +
                                  "MAXCYCLICITY = 0.15");
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