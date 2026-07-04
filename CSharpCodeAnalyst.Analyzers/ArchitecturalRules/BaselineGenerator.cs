using System.Text;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

/// <summary>
///     Turns the violations of a rule analysis into ALLOW rules ("baseline").
///     Accepting the current state as a baseline lets a team introduce rules into an existing
///     code base: every current violation is frozen as an explicit exception, so only *new*
///     violations are reported from then on.
/// </summary>
public static class BaselineGenerator
{
    /// <summary>
    ///     Builds the ALLOW lines that suppress every given violation. Each violating relationship
    ///     becomes one exact "ALLOW: source -> target" line, grouped by the originating rule.
    ///     ALLOW pairs already present in <paramref name="existingRulesText" /> are skipped, so
    ///     the operation is idempotent. Returns an empty string when there is nothing to add.
    /// </summary>
    public static string GenerateAllowRules(
        IReadOnlyList<Violation> violations,
        CodeGraph.Graph.CodeGraph graph,
        string existingRulesText)
    {
        var existingAllows = GetExistingAllowPairs(existingRulesText);
        var alreadyAdded = new HashSet<(string, string)>();
        var sb = new StringBuilder();

        foreach (var violation in violations)
        {
            var lines = new List<string>();

            foreach (var relationship in violation.ViolatingRelationships)
            {
                if (!graph.Nodes.TryGetValue(relationship.SourceId, out var source) ||
                    !graph.Nodes.TryGetValue(relationship.TargetId, out var target))
                {
                    continue;
                }

                var pair = (source.FullName, target.FullName);
                if (existingAllows.Contains(pair) || !alreadyAdded.Add(pair))
                {
                    continue;
                }

                lines.Add($"ALLOW: {source.FullName} -> {target.FullName}");
            }

            if (lines.Count > 0)
            {
                // Optional origin comment: which rule these exceptions belong to.
                sb.AppendLine($"// {violation.Rule.RuleText}");
                foreach (var line in lines)
                {
                    sb.AppendLine(line);
                }
            }
        }

        return sb.ToString();
    }

    private static HashSet<(string Source, string Target)> GetExistingAllowPairs(string existingRulesText)
    {
        try
        {
            return RuleParser.ParseRules(existingRulesText)
                .OfType<AllowRule>()
                .Select(a => (a.Source, a.Target))
                .ToHashSet();
        }
        catch (FormatException)
        {
            // The text was validated before the baseline button became enabled, but the user may
            // have edited it into an invalid state since. Fall back to no known exceptions rather
            // than failing - duplicates are harmless.
            return [];
        }
    }
}
