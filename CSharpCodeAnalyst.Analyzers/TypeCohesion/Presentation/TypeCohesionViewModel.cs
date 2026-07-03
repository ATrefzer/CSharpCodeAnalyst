using System.Collections.ObjectModel;
using System.Windows;
using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.AnalyzerSdk.Contracts;
using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.AnalyzerSdk.Messages;
using CSharpCodeAnalyst.AnalyzerSdk.Search;
using CSharpCodeAnalyst.AnalyzerSdk.Wpf;
using CSharpCodeAnalyst.CodeGraph.Algorithms.Partitioning;

namespace CSharpCodeAnalyst.Analyzers.TypeCohesion.Presentation;

internal class TypeCohesionViewModel : Table
{
    private readonly IPublisher _messaging;
    private readonly ObservableCollection<TableRow> _rows;

    internal TypeCohesionViewModel(List<TypeCohesionInfo> infos, IPublisher messaging)
    {
        _messaging = messaging;
        var rows = infos.Select(i => new TypeCohesionRowViewModel(i));
        _rows = new ObservableCollection<TableRow>(rows);
    }

    public override IEnumerable<TableColumnDefinition> GetColumns()
    {
        return new List<TableColumnDefinition>
        {
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_TypeCohesion_Class,
                PropertyName = nameof(TypeCohesionRowViewModel.Name)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_TypeCohesion_Partitions,
                PropertyName = nameof(TypeCohesionRowViewModel.Partitions)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_TypeCohesion_Members,
                PropertyName = nameof(TypeCohesionRowViewModel.Members)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_TypeCohesion_LargestPartition,
                PropertyName = nameof(TypeCohesionRowViewModel.LargestShare),

                // Sort by the numeric value, not the formatted percentage string.
                SortMemberName = nameof(TypeCohesionRowViewModel.LargestShareValue)
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
                Header = Strings.JumpToCode,
                Command = new WpfCommand<TypeCohesionRowViewModel>(JumpToCode, CanJumpToCode)
            },
            new CommandDefinition
            {
                Header = Strings.ShowPartitions,
                Command = new WpfCommand<TypeCohesionRowViewModel>(ShowPartitions)
            }
        ];
    }

    public override bool CanFilter => true;

    /// <summary>Filters by class name using the same search expression as the Advanced Search.</summary>
    public override ObservableCollection<TableRow> Filter(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return _rows;
        }

        var expression = SearchExpressionFactory.CreateSearchExpression(searchText);
        var filtered = _rows
            .Cast<TypeCohesionRowViewModel>()
            .Where(row => expression.Evaluate(row.Element));
        return new ObservableCollection<TableRow>(filtered);
    }

    private void ShowPartitions(TypeCohesionRowViewModel row)
    {
        // Base-aware, to match the partition count shown in the table.
        _messaging.Publish(new ShowPartitionsRequest(row.Element, true));
    }

    private static bool CanJumpToCode(TypeCohesionRowViewModel row)
    {
        return row.Element.SourceLocations.Count > 0;
    }

    private void JumpToCode(TypeCohesionRowViewModel row)
    {
        _messaging.Publish(new OpenSourceLocationRequest(row.Element.SourceLocations[0]));
    }
}
