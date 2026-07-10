using System.Globalization;
using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Metrics;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

/// <summary>
///     Limits the size of a single method, measured in code lines (blank and comment-only lines
///     excluded). Methods are the only elements we collect source metrics for today; the rule is
///     written against any element that has a <see cref="MemberMetrics.CodeLines" /> value, so it
///     widens automatically once more element kinds are measured.
///     Syntax: MAXLINES = 50 | MAXLINES: MyApp.Business.** = 50
/// </summary>
public class MaxLinesRule : CodeElementMetricRule
{
    public const string RuleKeyword = "MAXLINES";

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
        get => int.MaxValue;
    }

    protected override bool AppliesTo(CodeElement element)
    {
        return element.ElementType is CodeElementType.Method;
    }

    protected override double? GetActualValue(MemberMetrics metrics)
    {
        return metrics.CodeLines;
    }

    public override string FormatValue(double value)
    {
        return string.Format(Strings.ArchitecturalRules_MaxLines_Value, value.ToString("0.##", CultureInfo.InvariantCulture));
    }
}
