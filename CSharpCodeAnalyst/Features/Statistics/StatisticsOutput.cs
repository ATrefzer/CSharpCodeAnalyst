using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.Attributes;

namespace CSharpCodeAnalyst.Features.Statistics;

internal class StatisticsOutput(string statistic, string value) : IStatistic
{
    [DisplayColumn(Header = nameof(Statistic))]
    public string Statistic { get; set; } = statistic;

    [DisplayColumn(Header = nameof(Value))]
    public string Value { get; set; } = value;
}