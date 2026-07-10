namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     Common base of all architectural rules. A rule is one line of the rules text.
///     There are two kinds of rules, and they are validated in completely different ways:
///     <see cref="DependencyRule" /> constrains the relationships between code elements,
///     <see cref="MetricRule" /> constrains a measured value of the whole system.
/// </summary>
public abstract class RuleBase
{
    public string RuleText { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string Description { get; set; } = string.Empty;
}
