using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;
using Metrics = CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;

namespace CSharpCodeAnalyst.Analyzers.SystemMetrics.Presentation;

internal class SystemMetricsViewModel : Table
{
    private readonly ObservableCollection<TableRow> _rows;

    internal SystemMetricsViewModel(Metrics.SystemMetrics metrics)
    {
        _rows =
        [
            new SystemMetricRowViewModel(
                Strings.SystemMetrics_PropagationCost_Name,
                metrics.PropagationCost.ToString("P1", CultureInfo.InvariantCulture),
                Strings.SystemMetrics_PropagationCost_Description),

            new SystemMetricRowViewModel(
                Strings.SystemMetrics_Cyclicity_Name,
                metrics.Cyclicity.ToString("P1", CultureInfo.InvariantCulture),
                Strings.SystemMetrics_Cyclicity_Description),

            new SystemMetricRowViewModel(
                Strings.SystemMetrics_FeedbackDensity_Name,
                metrics.FeedbackDensity.ToString("P1", CultureInfo.InvariantCulture),
                Strings.SystemMetrics_FeedbackDensity_Description),

            new SystemMetricRowViewModel(
                Strings.SystemMetrics_TypesAnalyzed_Name,
                metrics.TypeCount.ToString(CultureInfo.InvariantCulture),
                Strings.SystemMetrics_TypesAnalyzed_Description),

            new SystemMetricRowViewModel(
                Strings.SystemMetrics_TypeDependencies_Name,
                metrics.TypeDependencyCount.ToString(CultureInfo.InvariantCulture),
                Strings.SystemMetrics_TypeDependencies_Description)
        ];
    }

    public override IEnumerable<TableColumnDefinition> GetColumns()
    {
        return new List<TableColumnDefinition>
        {
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_SystemMetrics_Metric,
                PropertyName = nameof(SystemMetricRowViewModel.Metric)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_SystemMetrics_Value,
                PropertyName = nameof(SystemMetricRowViewModel.Value),
                Width = 90
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_SystemMetrics_Description,
                PropertyName = nameof(SystemMetricRowViewModel.Description)
            }
        };
    }

    public override ObservableCollection<TableRow> GetData()
    {
        return _rows;
    }

    public override DataTemplate? GetRowDetailsTemplate()
    {
        return null;
    }
}
