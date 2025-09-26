using Contracts.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     Isolates source pattern from any external dependencies
///     Syntax: ISOLATE: Source
/// </summary>
public class IsolateRule : RuleBase
{
    public override List<Relationship> ValidateRule(
        HashSet<string> sourceIds,
        HashSet<string> targetIds,
        IEnumerable<Relationship> allRelationships)
    {
        var violations = new List<Relationship>();

        foreach (var relationship in allRelationships)
        {
            // ISOLATE violation: source uses anything outside of itself
            if (sourceIds.Contains(relationship.SourceId) && !sourceIds.Contains(relationship.TargetId))
            {
                violations.Add(relationship);
            }
        }

        return violations;
    }
}