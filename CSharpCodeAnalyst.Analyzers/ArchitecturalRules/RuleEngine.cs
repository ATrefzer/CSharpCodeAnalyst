using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;
using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.CodeGraph.Algorithms.Cycles;
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

    /// <summary>
    ///     The dependencies whose target is internal code. RESTRICT and ISOLATE validate against
    ///     these: framework and NuGet usage is not an architecture violation, otherwise every call
    ///     into System.* would break such a rule once external code is part of the graph. DENY keeps
    ///     the full list, so specific external usage can still be forbidden explicitly.
    /// </summary>
    private readonly List<Relationship> _dependenciesToInternal;

    private readonly CodeGraph.Graph.CodeGraph _graph;
    private readonly MetricStore _metricStore;
    private readonly HashSet<string> _reportedWarnings = [];
    private readonly RuleAnalysisResult _result = new();

    private RuleEngine(CodeGraph.Graph.CodeGraph graph, MetricStore metricStore)
    {
        _graph = graph;
        _metricStore = metricStore;
        _dependencies = graph.GetAllRelationships().Where(r => r.Type.IsDependency()).ToList();
        _dependenciesToInternal = _dependencies
            .Where(r => graph.Nodes.TryGetValue(r.TargetId, out var target) && !target.IsExternal)
            .ToList();
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
        EvaluateRestrictRules(rules.OfType<RestrictRule>().ToList());
        EvaluateNoCyclesRules(rules.OfType<NoCyclesRule>().ToList());
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

            AddViolation(isolateRule, isolateRule.ValidateRule(sourceIds, [], _dependenciesToInternal));
        }
    }

    /// <summary>
    ///     RESTRICT rules with overlapping sources widen each other, so they are evaluated as one group
    ///     against the union of their targets. The group, not one of its rules, names the violation.
    /// </summary>
    private void EvaluateRestrictRules(IReadOnlyList<RestrictRule> restrictRules)
    {
        foreach (var (sourceIds, rules) in GroupOverlappingRestrictRules(restrictRules))
        {
            var restrictGroup = new RestrictRuleGroup(rules);

            // Dependencies inside the source patterns are always allowed implicitly(!).
            var allowedTargetIds = new HashSet<string>(sourceIds);
            foreach (var restrictRule in rules)
            {
                allowedTargetIds.UnionWith(Resolve(restrictRule.Target, restrictRule.RuleText));
            }

            restrictGroup.AllowedTargetIds = allowedTargetIds;

            AddViolation(restrictGroup, restrictGroup.ValidateRule(sourceIds, [], _dependenciesToInternal));
        }
    }

    /// <summary>
    ///     Merges RESTRICT rules whose resolved source sets intersect - into one group.
    ///     Grouping on the resolved sets rather than the pattern text also catches nested patterns like
    ///     "A.**" and "A.B.**". The resulting groups have pairwise disjoint source sets, and the rules
    ///     of a group keep the order they were written in.
    /// </summary>
    private List<(HashSet<string> SourceIds, List<RestrictRule> Rules)> GroupOverlappingRestrictRules(
        IReadOnlyList<RestrictRule> restrictRules)
    {
        var resolvedSources = restrictRules.Select(r => Resolve(r.Source, r.RuleText)).ToList();

        // Merge the resolved source sets into disjoint partitions. Folding a new set into every
        // partition it overlaps also merges partitions that only become connected through it.
        var partitions = new List<HashSet<string>>();
        foreach (var sourceIds in resolvedSources.Where(s => s.Count > 0))
        {
            var merged = new HashSet<string>(sourceIds);
            for (var i = partitions.Count - 1; i >= 0; i--)
            {
                if (partitions[i].Overlaps(merged))
                {
                    merged.UnionWith(partitions[i]);
                    partitions.RemoveAt(i);
                }
            }

            partitions.Add(merged);
        }

        // Find the rules.
        var groups = new List<(HashSet<string> SourceIds, List<RestrictRule> Rules)>();
        foreach (var partition in partitions)
        {
            var rules = restrictRules.Where((_, i) => partition.Overlaps(resolvedSources[i])).ToList();
            groups.Add((partition, rules));
        }

        // A rule whose source matches nothing forms an inert group of its own, so its target is
        // still resolved - and warned about when dead - like any other rule's.
        for (var i = 0; i < restrictRules.Count; i++)
        {
            if (resolvedSources[i].Count == 0)
            {
                groups.Add((resolvedSources[i], [restrictRules[i]]));
            }
        }

        return groups;
    }

    /// <summary>
    ///     NOCYCLES runs the same cycle search as the interactive Cycles view, so the rule enforces
    ///     exactly what that view shows - including cycles that only exist between namespaces and
    ///     are invisible on the plain type graph. The search runs once and only when at least one
    ///     NOCYCLES rule exists; each violation is one cycle group. A cycle is a property of the
    ///     whole group, not of a single edge, so ALLOW exceptions do not apply.
    /// </summary>
    private void EvaluateNoCyclesRules(IReadOnlyList<NoCyclesRule> noCyclesRules)
    {
        if (noCyclesRules.Count == 0)
        {
            return;
        }

        var cycleGroups = CycleFinder.FindCycleGroups(_graph);

        foreach (var rule in noCyclesRules)
        {
            // The path always means the element and everything below it.
            var subtree = ResolveSubtree(rule.Source, rule.RuleText);

            foreach (var cycleGroup in cycleGroups.Where(g => ViolatesRule(g, subtree)))
            {
                var description = string.Format(
                    Strings.Analyzer_ArchitecturalRules_CycleFound,
                    rule.DisplayName,
                    cycleGroup.Name,
                    cycleGroup.Vertices.Count);

                _result.Violations.Add(new Violation(rule, cycleGroup.Vertices, description, cycleGroup.Name));
            }
        }
    }

    /// <summary>Whether a cycle group violates a NOCYCLES rule covering the given subtree.</summary>
    private static bool ViolatesRule(CycleGroup cycleGroup, HashSet<string> subtree)
    {
        // Mutual recursion between members of one type is a code pattern, not an architecture
        // violation. The search graph lifts every edge to peer rank, so a member-level cycle
        // group always lives inside a single type - only container cycles (types, namespaces)
        // count for the rule.
        if (cycleGroup.Vertices.All(v => CodeElementClassifier.GetContainerLevel(v.ElementType) == 0))
        {
            return false;
        }

        // The rule owns only the cycles that lie completely below its element. A cycle that is
        // only partly below it belongs to a rule on a higher element or to the boundary rules
        // (DENY / RESTRICT).
        return cycleGroup.Vertices.All(v => subtree.Contains(v.Id));
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

    /// <summary>Like <see cref="Resolve" />, but the path always means the whole subtree (NOCYCLES).</summary>
    private HashSet<string> ResolveSubtree(string path, string ruleText)
    {
        var ids = PatternMatcher.ResolveSubtree(path, _graph);
        if (ids.Count == 0)
        {
            AddWarning(string.Format(Strings.Analyzer_ArchitecturalRules_PatternNoMatch, ruleText, path));
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