namespace CSharpCodeAnalyst.MetricArea;

internal class MetricOutput(string metric, string value) : IMetric
{
    [DisplayColumn(Header = "Metric")] public string Metric { get; set; } = metric;

    [DisplayColumn(Header = "Value")] public string Value { get; set; } = value;
}