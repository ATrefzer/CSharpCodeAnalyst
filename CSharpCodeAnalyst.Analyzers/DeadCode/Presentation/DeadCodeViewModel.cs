using System.Collections.ObjectModel;
using System.Windows;
using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.AnalyzerSdk.Contracts;
using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.AnalyzerSdk.Messages;
using CSharpCodeAnalyst.AnalyzerSdk.Wpf;
using CSharpCodeAnalyst.CodeGraph.Algorithms.DeadCode;
using CSharpCodeAnalyst.CodeGraph.Search;

namespace CSharpCodeAnalyst.Analyzers.DeadCode.Presentation;

internal class DeadCodeViewModel : Table
{
    private readonly IPublisher _messaging;
    private readonly ObservableCollection<TableRow> _rows;

    internal DeadCodeViewModel(List<DeadCodeFinding> findings, IPublisher messaging)
    {
        _messaging = messaging;

        // Highest confidence first: that is the part of the result you can work through without checking
        // every entry by hand, so it belongs at the top before anyone touches a column header. The
        // analysis already sorts by name, which stays the tie breaker within a confidence band.
        var rows = findings
            .OrderByDescending(f => f.Confidence)
            .Select(f => new DeadCodeRowViewModel(f));
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
                Type = ColumnType.Text,
                Header = Strings.Column_DeadCode_Access,
                PropertyName = nameof(DeadCodeRowViewModel.Access),
                Width = 80
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_DeadCode_Confidence,
                PropertyName = nameof(DeadCodeRowViewModel.Confidence),
                Width = 80,

                // High (2) green, Medium (1) orange, Low (0) red - here a larger value is better.
                Rating = new ThresholdRating(2, 1, false),
                RatingValuePropertyName = nameof(DeadCodeRowViewModel.ConfidenceValue),
                SortMemberName = nameof(DeadCodeRowViewModel.ConfidenceValue)
            },
            new()
            {
                // Carries both the doubts (entry point, test code, attributes) and the explanation of a
                // contract finding. Empty means nothing speaks against deleting the element, and sorting
                // brings those rows together.
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
    ///     Filters by element name using the same search expression as the Advanced Search (camel-case,
    ///     OR via '|', AND via spaces, exclusion via a leading '-'). Exclusion is what makes a long result
    ///     usable: "-Strings. -Tests" drops whole groups of findings at once.
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
