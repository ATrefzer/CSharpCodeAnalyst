using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Analyzers.SystemMetrics.Presentation;

/// <summary>
///     One system metric as a table row: a name, its formatted value and a short explanation.
///     System metrics are single values, so the table has one row per metric rather than per element.
/// </summary>
public class SystemMetricRowViewModel(string metric, string value, string description) : TableRow
{
    public string Metric { get; } = metric;
    public string Value { get; } = value;
    public string Description { get; } = description;
}
