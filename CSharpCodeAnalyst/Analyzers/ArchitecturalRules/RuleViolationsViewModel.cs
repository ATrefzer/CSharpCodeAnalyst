using System.Collections.ObjectModel;
using System.Windows;
using Contracts.Graph;
using CSharpCodeAnalyst.Shared.Table;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

public class RuleViolationsViewModel : Table
{


    private readonly ObservableCollection<TableRow> _violations;

    public RuleViolationsViewModel(List<Violation> violations, CodeGraph codeGraph)
    {
        Title = "Rule violations";
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
        var xamlTemplate = @"
                <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                              xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
                   <ItemsControl ItemsSource=""{Binding RelationshipDetails}"">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Grid Margin=""40 0 0 0"">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width=""Auto"" />
                                        <ColumnDefinition />
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Grid.Column=""1""
                                               Text=""{Binding Description}""
                                               Foreground=""Blue""
                                               TextWrapping=""Wrap""
                                               Cursor=""Hand""
                                               TextDecorations=""Underline"">
                                        <TextBlock.InputBindings>
                                            <MouseBinding MouseAction=""LeftClick""
                                                          Command=""{Binding DataContext.OpenSourceLocationCommand,
                              RelativeSource={RelativeSource AncestorType=ItemsControl}}""
                                                          CommandParameter=""{Binding }"" />
                                        </TextBlock.InputBindings>
                                    </TextBlock>
                                </Grid>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </DataTemplate>";

        return CreateDataTemplateFromString(xamlTemplate);
    }
}