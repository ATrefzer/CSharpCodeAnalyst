using System.Collections.ObjectModel;
using System.Windows;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.Search;
using CSharpCodeAnalyst.Shared.Wpf;

namespace CSharpCodeAnalyst.Features.Analyzers.MethodComplexity.Presentation;

internal class MethodComplexityViewModel : Table
{
    private readonly IPublisher _messaging;
    private readonly ObservableCollection<TableRow> _rows;

    internal MethodComplexityViewModel(List<MethodComplexityRowViewModel> rows, IPublisher messaging)
    {
        _messaging = messaging;
        _rows = new ObservableCollection<TableRow>(rows);
    }

    public override IEnumerable<TableColumnDefinition> GetColumns()
    {
        return new List<TableColumnDefinition>
        {
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_MethodComplexity_Method,
                PropertyName = nameof(MethodComplexityRowViewModel.Name)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_MethodComplexity_Code,
                PropertyName = nameof(MethodComplexityRowViewModel.Code)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_MethodComplexity_Logical,
                PropertyName = nameof(MethodComplexityRowViewModel.Logical)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_MethodComplexity_Comments,
                PropertyName = nameof(MethodComplexityRowViewModel.Comments)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_MethodComplexity_CommentRatio,
                PropertyName = nameof(MethodComplexityRowViewModel.CommentRatio),
                SortMemberName = nameof(MethodComplexityRowViewModel.CommentRatioValue)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_MethodComplexity_Complexity,
                PropertyName = nameof(MethodComplexityRowViewModel.Complexity)
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
                Command = new WpfCommand<MethodComplexityRowViewModel>(ShowInExplorer)
            }
        ];
    }

    public override bool CanFilter => true;

    /// <summary>Filters by method name using the same search expression as the Advanced Search.</summary>
    public override ObservableCollection<TableRow> Filter(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return _rows;
        }

        var expression = SearchExpressionFactory.CreateSearchExpression(searchText);
        var filtered = _rows
            .Cast<MethodComplexityRowViewModel>()
            .Where(row => expression.Evaluate(row.Element));
        return new ObservableCollection<TableRow>(filtered);
    }

    private void ShowInExplorer(MethodComplexityRowViewModel row)
    {
        _messaging.Publish(new AddNodeToGraphRequest(row.Element));
    }
}
