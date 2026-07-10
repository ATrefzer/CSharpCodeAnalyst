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

    /// <summary>
    ///     The keyword this rule is known by in the rules text, used wherever a violation names its
    ///     rule. Derived from the class name by default; override when the two differ.
    /// </summary>
    public virtual string DisplayName
    {
        get => GetType().Name.Replace("Rule", "").ToUpper();
    }
}
