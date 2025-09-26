namespace CSharpCodeAnalyst.Analyzers.ConsistencyRules;

public class ConsistencyRule
{
    public string RuleText { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}