using Contracts.Graph;
using CSharpCodeAnalyst.Analyzers.ConsistencyRules.Rules;

namespace CSharpCodeAnalyst.Analyzers.ConsistencyRules;

public class Violation
{
    public ConsistencyRuleBase Rule { get; set; } = null!;
    public List<Relationship> ViolatingRelationships { get; set; } = [];
    public string Description { get; set; } = string.Empty;

    public Violation(ConsistencyRuleBase rule, IEnumerable<Relationship> violatingRelationships)
    {
        Rule = rule;
        ViolatingRelationships = violatingRelationships.ToList();
        Description = GenerateDescription();
    }

    private string GenerateDescription()
    {
        var ruleType = Rule.GetType().Name.Replace("Rule", "").ToUpper();
        var count = ViolatingRelationships.Count;

        return $"{ruleType} rule violated: {Rule.RuleText} ({count} violation{(count != 1 ? "s" : "")})";
    }
}