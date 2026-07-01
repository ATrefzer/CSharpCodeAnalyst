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

    internal TypeDependenciesViewModel(List<TypeDependencyInfo> infos, IPublisher messaging)
    {
        _messaging = messaging;
        var rows = infos.Select(i => new TypeDependencyViewModel(i));
        _rows = new ObservableCollection<TableRow>(rows);
    }

    public override IEnumerable<TableColumnDefinition> GetColumns()
    {
        // Column order mirrors the axes: Fan-in, Blast radius and Score are the incoming
        // dependence at rising resolution (direct, transitive count, transitive weighted);
        // Fan-out is the outgoing direction.
        return new List<TableColumnDefinition>
        {
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_TypeDependencies_Rank,
                PropertyName = nameof(TypeDependencyViewModel.Rank),
                Width = 40
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_TypeDependencies_Element,
                PropertyName = nameof(TypeDependencyViewModel.Name)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_TypeDependencies_FanIn,
                PropertyName = nameof(TypeDependencyViewModel.FanIn)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_TypeDependencies_BlastRadius,
                PropertyName = nameof(TypeDependencyViewModel.BlastRadius)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_TypeDependencies_Score,
                PropertyName = nameof(TypeDependencyViewModel.Score),

                // Sort by the numeric value, not the formatted string.
                SortMemberName = nameof(TypeDependencyViewModel.ScoreValue)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_TypeDependencies_FanOut,
                PropertyName = nameof(TypeDependencyViewModel.FanOut)
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
