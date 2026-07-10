using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     A rule that constrains the dependencies between code elements. Its <see cref="Source" /> (and,
///     for most rules, its target) is a pattern that is resolved to code element ids before the rule
///     is validated. A violation of such a rule is a set of relationships.
/// </summary>
public abstract class DependencyRule : RuleBase
{
    public string Source { get; set; } = string.Empty;

    /// <summary>
    ///     Validates this rule against the given relationships and returns violating relationships
    /// </summary>
    public abstract List<Relationship> ValidateRule(
        HashSet<string> sourceIds,
        HashSet<string> targetIds,
        IEnumerable<Relationship> allRelationships);
}
