using System.Globalization;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     A rule that constrains a measured value rather than the dependencies between code elements.
///     Syntax: KEYWORD = value, optionally scoped by a pattern (see <see cref="CodeElementMetricRule" />).
///     <para>
///         There are two kinds: a <see cref="SystemMetricRule" /> constrains one value of the whole
///         code base, a <see cref="CodeElementMetricRule" /> constrains a value of every matching code
///         element. They differ in where the value comes from, whether they have a pattern, and in the
///         shape of their violation - which is why they are separate classes.
///     </para>
///     <para>
///         <b>Units:</b> <see cref="Threshold" /> and the measured value are expressed in the rule's own
///         unit (percent for a cyclicity rule, lines for a size rule), so that the number the user writes
///         is the number the rule compares. Conversion from the internal representation of the metric
///         happens exactly once, where the rule reads it.
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

    public override string DisplayName
    {
        get => Keyword;
    }

    /// <summary>Smallest / largest threshold that makes sense for this metric. Used to reject typos.</summary>
    public abstract double MinThreshold { get; }

    public abstract double MaxThreshold { get; }

    /// <summary>Renders a value of this metric for the user, including its unit.</summary>
    public abstract string FormatValue(double value);

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
    public virtual string CreateRuleText(double threshold)
    {
        return $"{Keyword} = {FormatThreshold(threshold)}";
    }

    protected static string FormatThreshold(double threshold)
    {
        return threshold.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
