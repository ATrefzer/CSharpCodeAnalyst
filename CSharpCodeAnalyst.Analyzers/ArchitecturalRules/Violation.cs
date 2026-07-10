using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

/// <summary>A code element that broke the threshold of a code element metric rule.</summary>
/// <param name="Value">The measured value, in the unit of the rule.</param>
public sealed record ElementMetricViolation(CodeElement Element, double Value);

/// <summary>
///     What a rule found. Depends on the kind of rule, and only one of the members
///     below is ever filled - a dependency rule points at relationships, a system metric rule at the
///     one value it measured, a code element metric rule at the elements that broke its threshold.
/// </summary>
public class Violation
{
    /// <summary>A violation of a dependency rule: the relationships that must not exist.</summary>
    public Violation(RuleBase rule, IEnumerable<Relationship> violatingRelationships)
    {
        Rule = rule;
        ViolatingRelationships = violatingRelationships.ToList();
        Description = GenerateDescription();
    }

    /// <summary>
    ///     A violation of a system metric rule. <paramref name="metricValue" /> is the one measured
    ///     value that broke the rule's threshold.
    /// </summary>
    public Violation(RuleBase rule, double metricValue, string description)
    {
        Rule = rule;
        MetricValue = metricValue;
        Description = description;
    }

    /// <summary>
    ///     A violation of a code element metric rule: every element whose measured value broke the
    ///     rule's threshold, together with that value.
    /// </summary>
    public Violation(RuleBase rule, IEnumerable<ElementMetricViolation> violatingElements, string description)
    {
        Rule = rule;
        ViolatingElements = violatingElements.ToList();
        Description = description;
    }

    public RuleBase Rule { get; }
    
    /// <summary>
    /// Only for the CLI output.
    /// </summary>
    public string Description { get; }

    /// <summary>Offending relationships of a dependency rule, empty for every other rule.</summary>
    public List<Relationship> ViolatingRelationships { get; } = [];

    /// <summary>Measured value of a system metric rule, <c>null</c> for every other rule.</summary>
    public double? MetricValue { get; }

    /// <summary>Offending elements of a code element metric rule, empty for every other rule.</summary>
    public List<ElementMetricViolation> ViolatingElements { get; } = [];

    private string GenerateDescription()
    {
        var count = ViolatingRelationships.Count;
        return $"{Rule.DisplayName} rule violated: {Rule.RuleText} ({count} violation{(count != 1 ? "s" : "")})";
    }
}
