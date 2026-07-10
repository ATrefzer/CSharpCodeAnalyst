using System.Globalization;

// Alias: the type name SystemMetrics collides with the CSharpCodeAnalyst.Analyzers.SystemMetrics namespace.
using Metrics = CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     A rule that constrains a single measured value of the whole system rather than the
///     dependencies between individual code elements. It has no source / target pattern, it always
///     applies to the complete code base, and ALLOW exceptions do not affect it.
///     Syntax: KEYWORD = value
///     <para>
///         All metric rules read their value from <see cref="Metrics.SystemMetrics" />, which the rule engine
///         computes once per run. A future rule that constrains a metric <em>per code element</em>
///         (e.g. "no class with more than 20 dependencies") does not belong here - it needs a pattern
///         and reports element-level violations, so it would be a sibling of this class.
///     </para>
///     <para>
///         <b>Units:</b> <see cref="Threshold" /> and the value returned by <see cref="GetActualValue" />
///         are expressed in the rule's own unit (percent for a cyclicity rule, for instance), so that
///         the number the user writes is the number the rule compares. Conversion from the internal
///         representation of the metric happens exactly once, in <see cref="GetActualValue" />.
///     </para>
/// </summary>
public abstract class MetricRule : RuleBase
{
    /// <summary>
    ///     Guards the threshold comparison against floating point noise: a cyclicity of 3/10 must not
    ///     violate "MAXCYCLICITY = 30".
    /// </summary>
    private const double Tolerance = 1e-9;

    /// <summary>Number of decimal places a threshold is written with (see <see cref="CreateBaselineThreshold" />).</summary>
    private const int BaselinePrecision = 2;

    /// <summary>The upper bound the user configured, in the unit of this rule.</summary>
    public double Threshold { get; set; }

    /// <summary>The keyword that introduces this rule in the rules text, e.g. "MAXCYCLICITY".</summary>
    public abstract string Keyword { get; }

    /// <summary>Smallest / largest threshold that makes sense for this metric. Used to reject typos.</summary>
    public abstract double MinThreshold { get; }

    public abstract double MaxThreshold { get; }

    /// <summary>The measured value, converted into the unit of <see cref="Threshold" />.</summary>
    public abstract double GetActualValue(Metrics.SystemMetrics metrics);

    /// <summary>Renders a value of this metric for the user, including its unit.</summary>
    public abstract string FormatValue(double value);

    public abstract string CreateDescription();

    public virtual bool IsViolated(double actualValue)
    {
        return actualValue > Threshold + Tolerance;
    }

    /// <summary>
    ///     The smallest threshold that accepts <paramref name="actualValue" />, rounded to the precision
    ///     a rule line is written with. Rounding <em>up</em> is essential: a threshold below the measured
    ///     value would make the freshly written baseline rule violated again.
    /// </summary>
    public double CreateBaselineThreshold(double actualValue)
    {
        var factor = Math.Pow(10, BaselinePrecision);
        return Math.Clamp(Math.Ceiling(actualValue * factor) / factor, MinThreshold, MaxThreshold);
    }

    /// <summary>The rule line for a given threshold. Always culture invariant, so a rules file stays portable.</summary>
    public string CreateRuleText(double threshold)
    {
        return $"{Keyword} = {threshold.ToString("0.##", CultureInfo.InvariantCulture)}";
    }
}
