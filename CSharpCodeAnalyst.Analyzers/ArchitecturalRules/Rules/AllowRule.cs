using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     Marks dependencies from source to target as allowed. An ALLOW rule never produces
///     violations itself; it suppresses matching violations reported by DENY / RESTRICT / ISOLATE rules.
///     Syntax: ALLOW: Source -> Target
/// </summary>
public class AllowRule : TargetedDependencyRule
{
    public override List<Relationship> ValidateRule(
        HashSet<string> sourceIds,
        HashSet<string> targetIds,
        IEnumerable<Relationship> allRelationships)
    {
        // ALLOW is an exception, not a constraint.
        return [];
    }
}
