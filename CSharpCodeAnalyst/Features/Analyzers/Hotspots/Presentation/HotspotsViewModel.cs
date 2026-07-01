using System.Collections.ObjectModel;
using System.Windows;
using CodeGraph.Algorithms.Metrics;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.Wpf;

namespace CSharpCodeAnalyst.Features.Analyzers.Hotspots.Presentation;

internal class HotspotsViewModel : Table
{
    private readonly ObservableCollection<TableRow> _rows;
    private readonly IPublisher _messaging;

    internal HotspotsViewModel(List<TypeHotspot> hotspots, IPublisher messaging)
    {
        _messaging = messaging;
        var rows = hotspots.Select(h => new HotspotRowViewModel(h));
        _rows = new ObservableCollection<TableRow>(rows);
    }

    public override IEnumerable<TableColumnDefinition> GetColumns()
    {
        return new List<TableColumnDefinition>
        {
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_Hotspots_Rank,
                PropertyName = nameof(HotspotRowViewModel.Rank),
                Width = 40
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_Hotspots_Element,
                PropertyName = nameof(HotspotRowViewModel.Name)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_Hotspots_FanIn,
                PropertyName = nameof(HotspotRowViewModel.FanIn)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_Hotspots_FanOut,
                PropertyName = nameof(HotspotRowViewModel.FanOut)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_Hotspots_Score,
                PropertyName = nameof(HotspotRowViewModel.Score),

                // Sort by the numeric value, not the formatted string.
                SortMemberName = nameof(HotspotRowViewModel.ScoreValue)
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

    public override List<CommandDefinition> GetCommands()
    {
        return
        [
            new CommandDefinition
            {
                Header = Strings.CopyToExplorerGraph_MenuItem,
                Command = new WpfCommand<HotspotRowViewModel>(ShowInExplorer)
            }
        ];
    }

    private void ShowInExplorer(HotspotRowViewModel row)
    {
        _messaging.Publish(new AddNodeToGraphRequest(row.Element));
    }
}
