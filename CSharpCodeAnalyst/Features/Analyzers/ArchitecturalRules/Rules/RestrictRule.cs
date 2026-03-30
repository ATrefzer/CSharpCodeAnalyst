using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     Restricts source to only depend on target patterns (all others are denied)
///     Syntax: RESTRICT: Source -> Target
///     Note: Multiple RESTRICT rules with same source are grouped together
/// </summary>
public class RestrictRule : RuleBase
{
    public string Target { get; set; } = string.Empty;

    public override List<Relationship> ValidateRule(
        HashSet<string> sourceIds,
        HashSet<string> targetIds,
        IEnumerable<Relationship> allRelationships)
    {
        // RESTRICT rules are handled in groups, not individually
        // This method should not be called directly for RestrictRules
        throw new InvalidOperationException("RestrictRule validation must be done through RestrictRuleGroup");
    }
}