using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Metrics;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     A metric rule that constrains a value of every matching code element. Its violation carries the
///     offending elements together with their measured values.
///     Syntax: KEYWORD = value | KEYWORD: Pattern = value
///     <para>
///         The source metrics exist only for a subset of the graph - today only for methods with a body.
///         An element the rule cannot measure is <em>not applicable</em> rather than compliant or
///         violating: an abstract method has no body, so a size limit says nothing about it.
///         <see cref="Measure" /> answers "not applicable" with <c>null</c>, which is why it is the one
///         entry point the engine uses; a subclass only fills in the two pieces that vary.
///     </para>
/// </summary>
public abstract class CodeElementMetricRule : MetricRule
{
    /// <summary>The pattern that scopes this rule, empty when the rule applies to the whole graph.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    ///     The measured value of the element in the unit of <see cref="MetricRule.Threshold" />, or
    ///     <c>null</c> when this rule cannot say anything about the element - either because the rule is
    ///     not about that kind of element, or because the metric was not collected for it.
    /// </summary>
    public double? Measure(CodeElement element, MetricStore metricStore)
    {
        if (!AppliesTo(element))
        {
            return null;
        }

        var metrics = metricStore.TryGet(element.Id);
        return metrics is null ? null : GetActualValue(metrics);
    }

    /// <summary>Whether this rule is about the given element at all, independent of available metrics.</summary>
    protected abstract bool AppliesTo(CodeElement element);

    /// <summary>Reads the value out of the collected metrics, converted into the unit of this rule.</summary>
    protected abstract double? GetActualValue(MemberMetrics metrics);

    public override string CreateRuleText(double threshold)
    {
        return string.IsNullOrEmpty(Source)
            ? $"{Keyword} = {FormatThreshold(threshold)}"
            : $"{Keyword}: {Source} = {FormatThreshold(threshold)}";
    }
}
