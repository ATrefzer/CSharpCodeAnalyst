using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     Denies dependencies from source to target patterns
///     Syntax: DENY: Source -> Target
/// </summary>
public class DenyRule : RuleBase
{
    public string Target { get; set; } = string.Empty;

    public override List<Relationship> ValidateRule(
        HashSet<string> sourceIds,
        HashSet<string> targetIds,
        IEnumerable<Relationship> allRelationships)
    {
        var violations = new List<Relationship>();

        foreach (var relationship in allRelationships)
        {
            // DENY violation: source uses target (which is forbidden)
            if (sourceIds.Contains(relationship.SourceId) && targetIds.Contains(relationship.TargetId))
            {
                violations.Add(relationship);
            }
        }

        return violations;
    }
}