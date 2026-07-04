using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;
using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

/// <summary>
///     Executes architectural rules against a code graph.
///     Shared by the interactive analyzer and the command-line validation.
/// </summary>
public static class RuleEngine
{
    public static RuleAnalysisResult Execute(IReadOnlyCollection<RuleBase> rules, CodeGraph.Graph.CodeGraph graph)
    {
        var result = new RuleAnalysisResult();

        if (rules.Count == 0)
        {
            return result;
        }

        // Only real dependencies are subject to architectural rules. Descriptive edges like Handles
        // (event-handler wiring), and the non-dependency Containment / Bundled edges, are excluded
        var allRelationships = graph.GetAllRelationships()
            .Where(r => r.Type.IsDependency())
            .ToList();

        var denyRules = rules.OfType<DenyRule>().ToList();
        var isolateRules = rules.OfType<IsolateRule>().ToList();
        var restrictRules = rules.OfType<RestrictRule>().ToList();
        var allowRules = rules.OfType<AllowRule>().ToList();

        var reportedWarnings = new HashSet<string>();

        HashSet<string> Resolve(string pattern, string ruleText)
        {
            var ids = PatternMatcher.ResolvePattern(pattern, graph);
            if (ids.Count == 0)
            {
                var warning = string.Format(Strings.Analyzer_ArchitecturalRules_PatternNoMatch, ruleText, pattern);
                if (reportedWarnings.Add(warning))
                {
                    result.Warnings.Add(warning);
                }
            }

            return ids;
        }

        // Resolve ALLOW rules first. They never report violations themselves;
        // they suppress violations found by the other rules.
        var allowPairs = new List<(HashSet<string> SourceIds, HashSet<string> TargetIds)>();
        foreach (var allowRule in allowRules)
        {
            var sourceIds = Resolve(allowRule.Source, allowRule.RuleText);
            var targetIds = Resolve(allowRule.Target, allowRule.RuleText);
            if (sourceIds.Count > 0 && targetIds.Count > 0)
            {
                allowPairs.Add((sourceIds, targetIds));
            }
        }

        bool IsAllowed(Relationship relationship)
        {
            return allowPairs.Any(pair =>
                pair.SourceIds.Contains(relationship.SourceId) &&
                pair.TargetIds.Contains(relationship.TargetId));
        }

        // Applies the ALLOW exceptions before a violation is recorded so that the
        // violation description reflects the final relationship count.
        void AddViolation(RuleBase rule, List<Relationship> ruleViolations)
        {
            ruleViolations.RemoveAll(IsAllowed);
            if (ruleViolations.Count > 0)
            {
                result.Violations.Add(new Violation(rule, ruleViolations));
            }
        }

        // Process DENY rules (each is independent)
        foreach (var denyRule in denyRules)
        {
            var sourceIds = Resolve(denyRule.Source, denyRule.RuleText);
            var targetIds = Resolve(denyRule.Target, denyRule.RuleText);

            AddViolation(denyRule, denyRule.ValidateRule(sourceIds, targetIds, allRelationships));
        }

        // Process ISOLATE rules (each is independent)
        foreach (var isolateRule in isolateRules)
        {
            var sourceIds = Resolve(isolateRule.Source, isolateRule.RuleText);
            var emptyTargetIds = new HashSet<string>(); // Not used for ISOLATE

            AddViolation(isolateRule, isolateRule.ValidateRule(sourceIds, emptyTargetIds, allRelationships));
        }

        // Process RESTRICT rules (group by source)
        var restrictGroups = restrictRules.GroupBy(r => r.Source).ToList();
        foreach (var group in restrictGroups)
        {
            var restrictGroup = new RestrictRuleGroup(group.Key, group);
            var sourceIds = Resolve(group.Key, group.First().RuleText);

            // Collect all allowed target IDs from all rules in the group
            // References inside the source pattern are always allowed implicitly(!).
            var allowedTargetIds = new HashSet<string>(sourceIds);
            foreach (var restrictRule in group)
            {
                var targetIds = Resolve(restrictRule.Target, restrictRule.RuleText);
                allowedTargetIds.UnionWith(targetIds);
            }

            restrictGroup.AllowedTargetIds = allowedTargetIds;

            // Use first rule in group as representative for violation
            AddViolation(group.First(), restrictGroup.ValidateGroup(sourceIds, allRelationships));
        }

        return result;
    }
}
