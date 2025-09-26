using Contracts.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

public abstract class RuleBase
{
    public string RuleText { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Validates this rule against the given relationships and returns violating relationships
    /// </summary>
    public abstract List<Relationship> ValidateRule(
        HashSet<string> sourceIds,
        HashSet<string> targetIds,
        IEnumerable<Relationship> allRelationships);
}