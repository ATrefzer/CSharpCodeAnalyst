namespace CSharpCodeAnalyst.Analyzers.ConsistencyRules.Rules;

public abstract class ConsistencyRuleBase
{
    public string RuleText { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string Description { get; set; } = string.Empty;
}