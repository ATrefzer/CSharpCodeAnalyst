using System.Collections.ObjectModel;
using System.Windows;
using Contracts.Graph;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Presentation;

public class RuleViolationsViewModel : Table
{
    private readonly ObservableCollection<TableRow> _violations;

    public RuleViolationsViewModel(List<Violation> violations, CodeGraph codeGraph)
    {
        var violationViewModels = violations.Select(v => new RuleViolationViewModel(v, codeGraph));
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
                Header = "Rule Type",
                PropertyName = nameof(RuleViolationViewModel.RuleType),
                IsExpandable = true
            },
            new()
            {
                Type = ColumnType.Text,
                Header = "Source",
                PropertyName = nameof(RuleViolationViewModel.Source)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = "Target",
                PropertyName = nameof(RuleViolationViewModel.Target)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = "Violations",
                PropertyName = nameof(RuleViolationViewModel.ViolationCount)
            }
        };
    }

    public override ObservableCollection<TableRow> GetData()
    {
        return _violations;
    }

    public override DataTemplate? GetRowDetailsTemplate()
    {
        var uri = new Uri(
            "/CSharpCodeAnalyst;component/Analyzers/ArchitecturalRules/Presentation/RelationshipViewModelTemplate.xaml",
            UriKind.Relative);
        return (DataTemplate)Application.LoadComponent(uri);
    }

}