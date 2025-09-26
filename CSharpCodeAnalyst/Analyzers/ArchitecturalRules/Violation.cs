using Contracts.Graph;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

public class Violation
{

    public Violation(RuleBase rule, IEnumerable<Relationship> violatingRelationships)
    {
        Rule = rule;
        ViolatingRelationships = violatingRelationships.ToList();
        Description = GenerateDescription();
    }

    public RuleBase Rule { get; set; } = null!;
    public List<Relationship> ViolatingRelationships { get; set; } = [];
    public string Description { get; set; } = string.Empty;

    private string GenerateDescription()
    {
        var ruleType = Rule.GetType().Name.Replace("Rule", "").ToUpper();
        var count = ViolatingRelationships.Count;

        return $"{ruleType} rule violated: {Rule.RuleText} ({count} violation{(count != 1 ? "s" : "")})";
    }
}