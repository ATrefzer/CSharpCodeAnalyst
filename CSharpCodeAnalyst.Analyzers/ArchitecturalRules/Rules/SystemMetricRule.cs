// Alias: the type name SystemMetrics collides with the CSharpCodeAnalyst.Analyzers.SystemMetrics namespace.

using Metrics = CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     A metric rule that constrains a single value of the whole code base. It has no pattern, it
///     always applies, and ALLOW exceptions do not affect it. Its violation carries the one measured
///     value that broke the threshold.
/// </summary>
public abstract class SystemMetricRule : MetricRule
{
    /// <summary>
    ///     The measured value, converted into the unit of <see cref="MetricRule.Threshold" />. A system
    ///     metric always exists, so unlike <see cref="CodeElementMetricRule.Measure" /> this never
    ///     answers "not applicable".
    /// </summary>
    public abstract double Measure(Metrics.SystemMetrics metrics);
}
