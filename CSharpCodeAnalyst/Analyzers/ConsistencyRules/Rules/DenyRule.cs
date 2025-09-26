namespace CSharpCodeAnalyst.Analyzers.ConsistencyRules.Rules;

/// <summary>
/// Denies dependencies from source to target patterns
/// Syntax: DENY: Source -> Target
/// </summary>
public class DenyRule : ConsistencyRuleBase
{
    public string Target { get; set; } = string.Empty;
}