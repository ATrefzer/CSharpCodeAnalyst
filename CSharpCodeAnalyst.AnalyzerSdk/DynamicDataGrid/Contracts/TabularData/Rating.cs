namespace CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;

/// <summary>
///     Qualitative rating of a metric value. <see cref="Neutral" /> means "no opinion" and is
///     rendered without any highlight.
/// </summary>
public enum RatingLevel
{
    Neutral,
    Good,
    Warning,
    Bad
}

/// <summary>
///     Optional evaluation a metric can bring along: it maps a numeric value to a qualitative
///     <see cref="RatingLevel" />. The grid uses it to colour the corresponding cell background.
/// </summary>
public interface IMetricRating
{
    RatingLevel Evaluate(double value);
}

/// <summary>
///     Threshold-based rating - the common case. By default larger values are worse (complexity,
///     lines of code): <c>value &lt;= goodMax</c> is good, <c>value &lt;= warningMax</c> is a
///     warning, anything above is bad. Set <paramref name="higherIsWorse" /> to <c>false</c> for
///     metrics where smaller is worse (e.g. cohesion, comment ratio); the thresholds are then read
///     as lower bounds (<c>value &gt;= goodMax</c> is good, ...).
/// </summary>
public sealed class ThresholdRating(double goodMax, double warningMax, bool higherIsWorse = true) : IMetricRating
{
    public RatingLevel Evaluate(double value)
    {
        if (higherIsWorse)
        {
            if (value <= goodMax) { return RatingLevel.Good; }

            return value <= warningMax ? RatingLevel.Warning : RatingLevel.Bad;
        }

        if (value >= goodMax) { return RatingLevel.Good; }

        return value >= warningMax ? RatingLevel.Warning : RatingLevel.Bad;
    }
}
