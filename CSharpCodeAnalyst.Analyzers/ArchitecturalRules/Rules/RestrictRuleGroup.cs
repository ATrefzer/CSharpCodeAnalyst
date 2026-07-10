using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     All RESTRICT rules with the same source pattern, evaluated as one rule. RESTRICT rules widen
///     each other: "A may only depend on B" and "A may only depend on C" together mean "A may only
///     depend on B or C". Checking them separately would make each rule report the other's permitted
///     dependencies as violations, so the group - not the single rule - is what gets validated and
///     what carries the resulting violation.
/// </summary>
public class RestrictRuleGroup : DependencyRule
{
    public RestrictRuleGroup(string source, IEnumerable<RestrictRule> rules)
    {
        Source = source;
        Rules = rules.ToList();
        RuleText = string.Join("; ", Rules.Select(r => r.RuleText));
    }

    private List<RestrictRule> Rules { get; }

    /// <summary>The union of the target patterns of all rules in the group.</summary>
    public IEnumerable<string> Targets
    {
        get => Rules.Select(r => r.Target);
    }

    public HashSet<string> AllowedTargetIds { get; set; } = [];

    public override string DisplayName
    {
        get => "RESTRICT";
    }

    /// <summary>
    ///     A relationship violates the group when it leaves the source and does not end in any of the
    ///     allowed targets. <paramref name="targetIds" /> is unused: the targets of the whole group are
    ///     resolved by the engine into <see cref="AllowedTargetIds" /> before validation.
    /// </summary>
    public override List<Relationship> ValidateRule(
        HashSet<string> sourceIds,
        HashSet<string> targetIds,
        IEnumerable<Relationship> allRelationships)
    {
        var violations = new List<Relationship>();

        foreach (var relationship in allRelationships)
        {
            if (sourceIds.Contains(relationship.SourceId) && !AllowedTargetIds.Contains(relationship.TargetId))
            {
                violations.Add(relationship);
            }
        }

        return violations;
    }
}
