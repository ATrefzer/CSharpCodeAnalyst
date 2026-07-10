using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;
using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Metrics;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

/// <summary>
///     Executes architectural rules against a code graph and metrics.
///     Shared by the interactive analyzer and the command-line validation.
/// </summary>
public sealed class RuleEngine
{
    private readonly List<(HashSet<string> SourceIds, HashSet<string> TargetIds)> _allowPairs = [];

    /// <summary>The edges the rules are about: descriptive edges like Handles are not dependencies.</summary>
    private readonly List<Relationship> _dependencies;

    private readonly CodeGraph.Graph.CodeGraph _graph;
    private readonly MetricStore _metricStore;
    private readonly HashSet<string> _reportedWarnings = [];
    private readonly RuleAnalysisResult _result = new();

    private RuleEngine(CodeGraph.Graph.CodeGraph graph, MetricStore metricStore)
    {
        _graph = graph;
        _metricStore = metricStore;
        _dependencies = graph.GetAllRelationships().Where(r => r.Type.IsDependency()).ToList();
    }
    
    public static RuleAnalysisResult Execute(IReadOnlyCollection<RuleBase> rules, CodeGraph.Graph.CodeGraph graph, MetricStore metricStore)
    {
        if (rules.Count == 0)
        {
            return new RuleAnalysisResult();
        }

        return new RuleEngine(graph, metricStore).Run(rules);
    }

    private RuleAnalysisResult Run(IReadOnlyCollection<RuleBase> rules)
    {
        // ALLOW rules never report violations themselves, they suppress the violations of the other
        // rules - so they have to be resolved before anything is evaluated.
        ResolveAllowExceptions(rules.OfType<AllowRule>());

        EvaluateDenyRules(rules.OfType<DenyRule>());
        EvaluateIsolateRules(rules.OfType<IsolateRule>());
        EvaluateRestrictRules(rules.OfType<RestrictRule>());
        EvaluateSystemMetricRules(rules.OfType<SystemMetricRule>().ToList());
        EvaluateElementMetricRules(rules.OfType<CodeElementMetricRule>());

        return _result;
    }

    private void ResolveAllowExceptions(IEnumerable<AllowRule> allowRules)
    {
        foreach (var allowRule in allowRules)
        {
            var sourceIds = Resolve(allowRule.Source, allowRule.RuleText);
            var targetIds = Resolve(allowRule.Target, allowRule.RuleText);
            if (sourceIds.Count > 0 && targetIds.Count > 0)
            {
                _allowPairs.Add((sourceIds, targetIds));
            }
        }
    }

    /// <summary>Each DENY rule is independent of the others.</summary>
    private void EvaluateDenyRules(IEnumerable<DenyRule> denyRules)
    {
        foreach (var denyRule in denyRules)
        {
            var sourceIds = Resolve(denyRule.Source, denyRule.RuleText);
            var targetIds = Resolve(denyRule.Target, denyRule.RuleText);

            AddViolation(denyRule, denyRule.ValidateRule(sourceIds, targetIds, _dependencies));
        }
    }

    /// <summary>Each ISOLATE rule is independent of the others. It has no target pattern.</summary>
    private void EvaluateIsolateRules(IEnumerable<IsolateRule> isolateRules)
    {
        foreach (var isolateRule in isolateRules)
        {
            var sourceIds = Resolve(isolateRule.Source, isolateRule.RuleText);

            AddViolation(isolateRule, isolateRule.ValidateRule(sourceIds, [], _dependencies));
        }
    }

    /// <summary>
    ///     RESTRICT rules with the same source widen each other, so they are evaluated as one group
    ///     against the union of their targets. The group, not one of its rules, names the violation.
    /// </summary>
    private void EvaluateRestrictRules(IEnumerable<RestrictRule> restrictRules)
    {
        foreach (var group in restrictRules.GroupBy(r => r.Source))
        {
            var restrictGroup = new RestrictRuleGroup(group.Key, group);
            var sourceIds = Resolve(group.Key, restrictGroup.RuleText);

            // Dependencies inside the source pattern are always allowed implicitly(!).
            var allowedTargetIds = new HashSet<string>(sourceIds);
            foreach (var restrictRule in group)
            {
                allowedTargetIds.UnionWith(Resolve(restrictRule.Target, restrictRule.RuleText));
            }

            restrictGroup.AllowedTargetIds = allowedTargetIds;

            AddViolation(restrictGroup, restrictGroup.ValidateRule(sourceIds, [], _dependencies));
        }
    }

    /// <summary>
    ///     Metric rules are not about single relationships, so ALLOW exceptions do not apply to them.
    ///     All system metric rules read the same metrics object, which is computed once and only when
    ///     at least one such rule exists.
    /// </summary>
    private void EvaluateSystemMetricRules(IReadOnlyCollection<SystemMetricRule> systemMetricRules)
    {
        if (systemMetricRules.Count == 0)
        {
            return;
        }

        var metrics = SystemMetricsAnalysis.Calculate(_graph);
        foreach (var metricRule in systemMetricRules)
        {
            var actualValue = metricRule.Measure(metrics);
            if (!metricRule.IsViolated(actualValue))
            {
                continue;
            }

            var description = string.Format(
                Strings.Analyzer_ArchitecturalRules_MetricExceeded,
                metricRule.Keyword,
                metricRule.FormatValue(actualValue),
                metricRule.FormatValue(metricRule.Threshold));

            _result.Violations.Add(new Violation(metricRule, actualValue, description));
        }
    }

    private void EvaluateElementMetricRules(IEnumerable<CodeElementMetricRule> elementMetricRules)
    {
        foreach (var metricRule in elementMetricRules)
        {
            if (_metricStore.IsEmpty)
            {
                // The whole graph has no source metrics (an interface-only solution, say), so the rule
                // cannot be checked at all. Saying nothing would look like a pass. This is not the
                // per-element "no metric for this element" case below, which is simply not applicable.
                AddWarning(string.Format(Strings.Analyzer_ArchitecturalRules_NoSourceMetrics, metricRule.RuleText));
                continue;
            }

            var violatingElements = FindViolatingElements(metricRule);
            if (violatingElements.Count == 0)
            {
                continue;
            }

            var description = string.Format(
                Strings.Analyzer_ArchitecturalRules_ElementMetricExceeded,
                metricRule.Keyword,
                violatingElements.Count,
                metricRule.FormatValue(metricRule.Threshold));

            _result.Violations.Add(new Violation(metricRule, violatingElements, description));
        }
    }

    /// <summary>The offending elements, worst first.</summary>
    private List<ElementMetricViolation> FindViolatingElements(CodeElementMetricRule metricRule)
    {
        // An empty pattern scopes the rule to the whole graph.
        IEnumerable<string> scope = metricRule.Source.Length == 0
            ? _graph.Nodes.Keys
            : Resolve(metricRule.Source, metricRule.RuleText);

        var violatingElements = new List<ElementMetricViolation>();
        foreach (var elementId in scope)
        {
            var element = _graph.Nodes[elementId];

            // No value means the rule cannot say anything about this element (an abstract method,
            // say). That is neither compliant nor violating.
            var actualValue = metricRule.Measure(element, _metricStore);
            if (actualValue.HasValue && metricRule.IsViolated(actualValue.Value))
            {
                violatingElements.Add(new ElementMetricViolation(element, actualValue.Value));
            }
        }

        return violatingElements.OrderByDescending(v => v.Value).ToList();
    }

    /// <summary>
    ///     Resolves a pattern to code element ids, warning about a pattern that matches nothing - such a
    ///     rule has no effect, and a silently dead rule is worse than none.
    /// </summary>
    private HashSet<string> Resolve(string pattern, string ruleText)
    {
        var ids = PatternMatcher.ResolvePattern(pattern, _graph);
        if (ids.Count == 0)
        {
            AddWarning(string.Format(Strings.Analyzer_ArchitecturalRules_PatternNoMatch, ruleText, pattern));
        }

        return ids;
    }

    /// <summary>Reports a warning once, however many rules run into it.</summary>
    private void AddWarning(string warning)
    {
        if (_reportedWarnings.Add(warning))
        {
            _result.Warnings.Add(warning);
        }
    }

    /// <summary>
    ///     Applies the ALLOW exceptions before the violation is recorded, so that its relationship count
    ///     is the final one. A rule whose violations are all excepted does not appear in the result.
    /// </summary>
    private void AddViolation(RuleBase rule, List<Relationship> violatingRelationships)
    {
        violatingRelationships.RemoveAll(IsAllowed);
        if (violatingRelationships.Count > 0)
        {
            _result.Violations.Add(new Violation(rule, violatingRelationships));
        }
    }

    private bool IsAllowed(Relationship relationship)
    {
        return _allowPairs.Any(pair =>
            pair.SourceIds.Contains(relationship.SourceId) &&
            pair.TargetIds.Contains(relationship.TargetId));
    }
}