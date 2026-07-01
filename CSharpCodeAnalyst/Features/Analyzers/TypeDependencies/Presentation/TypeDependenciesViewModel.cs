using System.Collections.ObjectModel;
using System.Windows;
using CodeGraph.Algorithms.Metrics;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.Wpf;

namespace CSharpCodeAnalyst.Features.Analyzers.TypeDependencies.Presentation;

internal class TypeDependenciesViewModel : Table
{
    private readonly ObservableCollection<TableRow> _rows;
    private readonly IPublisher _messaging;

    internal TypeDependenciesViewModel(List<TypeHotspot> hotspots, IPublisher messaging)
    {
        _messaging = messaging;
        var rows = hotspots.Select(h => new TypeDependencyViewModel(h));
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
                PropertyName = nameof(TypeDependencyViewModel.Rank),
                Width = 40
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_Hotspots_Element,
                PropertyName = nameof(TypeDependencyViewModel.Name)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_Hotspots_FanIn,
                PropertyName = nameof(TypeDependencyViewModel.FanIn)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_Hotspots_FanOut,
                PropertyName = nameof(TypeDependencyViewModel.FanOut)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_Hotspots_Score,
                PropertyName = nameof(TypeDependencyViewModel.Score),

                // Sort by the numeric value, not the formatted string.
                SortMemberName = nameof(TypeDependencyViewModel.ScoreValue)
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
                Command = new WpfCommand<TypeDependencyViewModel>(ShowInExplorer)
            }
        ];
    }

    private void ShowInExplorer(TypeDependencyViewModel row)
    {
        _messaging.Publish(new AddNodeToGraphRequest(row.Element));
    }
}
