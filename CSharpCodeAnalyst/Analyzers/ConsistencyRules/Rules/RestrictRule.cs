namespace CSharpCodeAnalyst.Analyzers.ConsistencyRules.Rules;

/// <summary>
/// Restricts source to only depend on target patterns (all others are denied)
/// Syntax: RESTRICT: Source -> Target
/// </summary>
public class RestrictRule : ConsistencyRuleBase
{
    public string Target { get; set; } = string.Empty;
}