using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

/// <summary>
///     Removes rule lines that currently have no effect because one of their patterns matches no
///     code element. The operation is intentionally <b>behaviour-preserving</b>: it only drops rules
///     whose removal cannot change the analysis result. In particular a RESTRICT rule with an
///     unmatched target is kept, because it still constrains its source ("may only depend on
///     itself") and removing it would weaken enforcement.
/// </summary>
public static class RuleCleaner
{
    public static (string CleanedText, int RemovedCount) RemoveUnusedRules(
        string rulesText,
        CodeGraph.Graph.CodeGraph graph)
    {
        if (string.IsNullOrEmpty(rulesText))
        {
            return (rulesText, 0);
        }

        // Split on '\n' and re-join on '\n' so the original line endings and any comment / blank
        // lines are preserved untouched.
        var lines = rulesText.Split('\n');
        var kept = new List<string>(lines.Length);
        var removed = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//"))
            {
                kept.Add(line);
                continue;
            }

            RuleBase rule;
            try
            {
                rule = RuleParser.ParseRule(trimmed);
            }
            catch (Exception)
            {
                // Not a rule we understand - never touch it.
                kept.Add(line);
                continue;
            }

            if (IsIneffective(rule, graph))
            {
                removed++;
                continue;
            }

            kept.Add(line);
        }

        return (string.Join("\n", kept), removed);
    }

    /// <summary>
    ///     A rule is ineffective when removing it cannot change the analysis result.
    /// </summary>
    private static bool IsIneffective(RuleBase rule, CodeGraph.Graph.CodeGraph graph)
    {
        // A scoped code element metric rule is dead when its pattern matches nothing.
        if (rule is CodeElementMetricRule elementMetricRule)
        {
            return elementMetricRule.Source.Length > 0 &&
                   PatternMatcher.ResolvePattern(elementMetricRule.Source, graph).Count == 0;
        }

        if (rule is not DependencyRule dependencyRule)
        {
            // A system metric rule has no pattern, it always applies.
            return false;
        }

        var sourceEmpty = PatternMatcher.ResolvePattern(dependencyRule.Source, graph).Count == 0;

        return rule switch
        {
            IsolateRule => sourceEmpty,

            // A RESTRICT with an unmatched target still restricts its source, so only an empty
            // source makes it a no-op. This case has to precede the targeted rules below.
            RestrictRule => sourceEmpty,

            // DENY and ALLOW need both ends to match to have any effect.
            TargetedDependencyRule targeted =>
                sourceEmpty || PatternMatcher.ResolvePattern(targeted.Target, graph).Count == 0,
            _ => false
        };
    }
}
