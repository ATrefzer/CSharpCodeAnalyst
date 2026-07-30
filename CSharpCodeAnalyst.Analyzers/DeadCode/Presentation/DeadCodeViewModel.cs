using System.Collections.ObjectModel;
using System.Windows;
using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.AnalyzerSdk.Contracts;
using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.AnalyzerSdk.Messages;
using CSharpCodeAnalyst.AnalyzerSdk.Search;
using CSharpCodeAnalyst.AnalyzerSdk.Wpf;
using CSharpCodeAnalyst.CodeGraph.Algorithms.DeadCode;

namespace CSharpCodeAnalyst.Analyzers.DeadCode.Presentation;

internal class DeadCodeViewModel : Table
{
    private readonly IPublisher _messaging;
    private readonly ObservableCollection<TableRow> _rows;

    internal DeadCodeViewModel(List<DeadCodeFinding> findings, IPublisher messaging)
    {
        _messaging = messaging;
        var rows = findings.Select(f => new DeadCodeRowViewModel(f));
        _rows = new ObservableCollection<TableRow>(rows);
    }

    public override bool CanFilter => true;

    public override IEnumerable<TableColumnDefinition> GetColumns()
    {
        return new List<TableColumnDefinition>
        {
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_DeadCode_Element,
                PropertyName = nameof(DeadCodeRowViewModel.Name)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_DeadCode_Kind,
                PropertyName = nameof(DeadCodeRowViewModel.Kind),
                Width = 90
            },
            new()
            {
                // Empty means nothing speaks against deleting it - sorting brings those rows together.
                Type = ColumnType.Text,
                Header = Strings.Column_DeadCode_Hint,
                PropertyName = nameof(DeadCodeRowViewModel.Hint)
            }
        };
    }

    public override ObservableCollection<TableRow> GetData()
    {
        return _rows;
    }

    /// <summary>
    ///     Filters by element name using the same search expression as the Advanced Search
    ///     (supports camel-case, OR via '|', AND via spaces).
    /// </summary>
    public override ObservableCollection<TableRow> Filter(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return _rows;
        }

        var expression = SearchExpressionFactory.CreateSearchExpression(searchText);
        var filtered = _rows
            .Cast<DeadCodeRowViewModel>()
            .Where(row => expression.Evaluate(row.Element));
        return new ObservableCollection<TableRow>(filtered);
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
                Command = new WpfCommand<DeadCodeRowViewModel>(JumpToCode, CanJumpToCode)
            },
            new CommandDefinition
            {
                Header = Strings.CopyToExplorerGraph_MenuItem,
                Command = new WpfCommand<DeadCodeRowViewModel>(ShowInExplorer)
            }
        ];
    }

    private void ShowInExplorer(DeadCodeRowViewModel row)
    {
        _messaging.Publish(new AddNodeToGraphRequest(row.Element));
    }

    private static bool CanJumpToCode(DeadCodeRowViewModel row)
    {
        return row.Element.SourceLocations.Count > 0;
    }

    private void JumpToCode(DeadCodeRowViewModel row)
    {
        _messaging.Publish(new OpenSourceLocationRequest(row.Element.SourceLocations[0]));
    }
}
