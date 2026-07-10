using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

/// <summary>A code element that broke the threshold of a code element metric rule.</summary>
/// <param name="Value">The measured value, in the unit of the rule.</param>
public sealed record ElementMetricViolation(CodeElement Element, double Value);

public class Violation
{

    public Violation(RuleBase rule, IEnumerable<Relationship> violatingRelationships)
    {
        Rule = rule;
        ViolatingRelationships = violatingRelationships.ToList();
        Description = GenerateDescription();
    }

    /// <summary>
    ///     A violation of a system metric rule. It is not backed by relationships;
    ///     <paramref name="metricValue" /> is the one measured value that broke the rule's threshold.
    /// </summary>
    public Violation(RuleBase rule, double metricValue, string description)
    {
        Rule = rule;
        ViolatingRelationships = [];
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
        ViolatingRelationships = [];
        ViolatingElements = violatingElements.ToList();
        Description = description;
    }

    public RuleBase Rule { get; set; }
    public List<Relationship> ViolatingRelationships { get; set; }
    public string Description { get; set; }

    /// <summary>Measured value of a system metric rule, <c>null</c> for every other rule.</summary>
    public double? MetricValue { get; }

    /// <summary>Offending elements of a code element metric rule, empty for every other rule.</summary>
    public List<ElementMetricViolation> ViolatingElements { get; } = [];

    private string GenerateDescription()
    {
        var ruleType = Rule.GetType().Name.Replace("Rule", "").ToUpper();
        var count = ViolatingRelationships.Count;

        return $"{ruleType} rule violated: {Rule.RuleText} ({count} violation{(count != 1 ? "s" : "")})";
    }
}