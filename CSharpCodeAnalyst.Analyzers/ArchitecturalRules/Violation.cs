using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;
using CSharpCodeAnalyst.Analyzers.Resources;
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
    ///     A violation of a NOCYCLES rule: one dependency cycle, carried as the participating
    ///     elements on the lifted cycle level - the same elements the Cycles view counts. Nobody
    ///     analyzes a cycle edge by edge in this table; the user identifies the group by name and
    ///     participant count and continues in the Cycles view.
    /// </summary>
    public Violation(RuleBase rule, IEnumerable<CodeElement> cycleElements, string description, string cycleName)
    {
        Rule = rule;
        CycleElements = cycleElements.ToList();
        Description = description;
        CycleName = cycleName;
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

    /// <summary>
    ///     Name of the violated cycle group, <c>null</c> for every rule but NOCYCLES. It is the same
    ///     name the Cycles view stamps on the group, so the user can find the cycle there and
    ///     analyze it further.
    /// </summary>
    public string? CycleName { get; }

    /// <summary>
    ///     Participants of a violated NOCYCLES rule on the lifted cycle level (namespaces, types),
    ///     empty for every other rule.
    /// </summary>
    public List<CodeElement> CycleElements { get; } = [];

    /// <summary>Measured value of a system metric rule, <c>null</c> for every other rule.</summary>
    public double? MetricValue { get; }

    /// <summary>Offending elements of a code element metric rule, empty for every other rule.</summary>
    public List<ElementMetricViolation> ViolatingElements { get; } = [];

    private string GenerateDescription()
    {
        return string.Format(
            Strings.Analyzer_ArchitecturalRules_DependencyViolation,
            Rule.DisplayName,
            Rule.RuleText,
            ViolatingRelationships.Count);
    }
}
