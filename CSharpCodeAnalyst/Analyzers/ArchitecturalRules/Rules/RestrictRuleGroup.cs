using Contracts.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     Groups multiple RESTRICT rules with the same source pattern
/// </summary>
public class RestrictRuleGroup
{

    public RestrictRuleGroup(string source, IEnumerable<RestrictRule> rules)
    {
        Source = source;
        Rules = rules.ToList();
    }

    private string Source { get; }
    private List<RestrictRule> Rules { get; }
    public HashSet<string> AllowedTargetIds { get; set; } = [];

    public List<Relationship> ValidateGroup(
        HashSet<string> sourceIds,
        IEnumerable<Relationship> allRelationships)
    {
        var violations = new List<Relationship>();

        foreach (var relationship in allRelationships)
        {
            // RESTRICT violation: source uses something not in the allowed targets
            if (sourceIds.Contains(relationship.SourceId) && !AllowedTargetIds.Contains(relationship.TargetId))
            {
                violations.Add(relationship);
            }
        }

        return violations;
    }

    public string GetDescription()
    {
        var count = Rules.Count;
        var targets = string.Join(", ", Rules.Select(r => r.Target));
        return $"RESTRICT group: {Source} may only depend on: {targets} ({count} rule{(count != 1 ? "s" : "")})";
    }
}