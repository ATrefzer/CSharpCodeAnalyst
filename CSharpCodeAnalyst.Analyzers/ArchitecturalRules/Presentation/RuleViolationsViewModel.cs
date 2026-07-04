using System.Collections.ObjectModel;
using System.Windows;
using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.AnalyzerSdk.Contracts;
using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Presentation;

public class RuleViolationsViewModel : Table
{
    private readonly ObservableCollection<TableRow> _violations;

    public RuleViolationsViewModel(List<Violation> violations, CodeGraph.Graph.CodeGraph codeGraph, IPublisher messaging)
    {
        var violationViewModels = violations.Select(v => new RuleViolationViewModel(v, codeGraph, messaging));
        _violations = new ObservableCollection<TableRow>(violationViewModels);
    }

    public override IEnumerable<TableColumnDefinition> GetColumns()
    {
        return new List<TableColumnDefinition>
        {
            new()
            {
                Type = ColumnType.Icon,
                Header = "",
                PropertyName = nameof(RuleViolationViewModel.ErrorIcon)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_ArchitecturalRules_RuleType,
                PropertyName = nameof(RuleViolationViewModel.RuleType),
                IsExpandable = true
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_ArchitecturalRules_Source,
                PropertyName = nameof(RuleViolationViewModel.Source)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_ArchitecturalRules_Target,
                PropertyName = nameof(RuleViolationViewModel.Target)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_ArchitecturalRules_Violations,
                PropertyName = nameof(RuleViolationViewModel.ViolationCount)
            }
        };
    }

    public override ObservableCollection<TableRow> GetData()
    {
        return _violations;
    }

    public override DataTemplate GetRowDetailsTemplate()
    {
        var uri = new Uri(
            "/CSharpCodeAnalyst.Analyzers;component/ArchitecturalRules/Presentation/RelationshipViewModelTemplate.xaml",
            UriKind.Relative);
        return (DataTemplate)Application.LoadComponent(uri);
    }
}