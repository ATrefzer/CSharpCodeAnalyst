using CSharpCodeAnalyst.Shared.Contracts;

namespace CSharpCodeAnalyst.Areas.MetricArea;

internal class MetricOutput(string metric, string value) : IMetric
{
    [DisplayColumn(Header = nameof(Metric))] public string Metric { get; set; } = metric;

    [DisplayColumn(Header = nameof(Value))] public string Value { get; set; } = value;
}