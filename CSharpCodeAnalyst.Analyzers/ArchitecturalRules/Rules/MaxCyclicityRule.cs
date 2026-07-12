using System.Globalization;

// Alias: the type name SystemMetrics collides with the CSharpCodeAnalyst.Analyzers.SystemMetrics namespace.
using Metrics = CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     Limits the cyclicity of the analyzed system: the share of internal types that sit inside a
///     dependency cycle. The threshold is given in percent, like the value the system metrics
///     analyzer displays.
///     Syntax: MAXCYCLICITY = 15
///     <para>
///         Measured on the plain type dependency graph. Cycles that only exist between namespaces
///         (two namespaces referencing each other through otherwise acyclic types) do not count
///         here - <see cref="NoCyclesRule" /> is the rule that sees those.
///     </para>
/// </summary>
public class MaxCyclicityRule : SystemMetricRule
{
    public const string RuleKeyword = "MAXCYCLICITY";

    public override string Keyword
    {
        get => RuleKeyword;
    }

    public override double MinThreshold
    {
        get => 0.0;
    }

    public override double MaxThreshold
    {
        get => 100.0;
    }

    public override double Measure(Metrics.SystemMetrics metrics)
    {
        // SystemMetrics reports the cyclicity as a share in [0,1], the rule speaks percent.
        return metrics.Cyclicity * 100.0;
    }

    public override string FormatValue(double value)
    {
        return $"{value.ToString("0.##", CultureInfo.InvariantCulture)} %";
    }
}
