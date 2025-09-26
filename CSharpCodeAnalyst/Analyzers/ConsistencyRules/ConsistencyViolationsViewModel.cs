using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Contracts.Graph;
using CSharpCodeAnalyst.Shared.Table;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Analyzers.ConsistencyRules;

public class ConsistencyViolationsViewModel : Table
{
  

    private readonly ObservableCollection<TableRow> _violations;

    public ConsistencyViolationsViewModel(List<Violation> violations, CodeGraph codeGraph)
    {
        Title = "Consistency Rule Violations";
        var violationViewModels = violations.Select(v => new ConsistencyViolationViewModel(v, codeGraph));
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
                PropertyName = nameof(ConsistencyViolationViewModel.ErrorIcon),
            },
            new()
            {
                Type = ColumnType.Text,
                Header = "Rule Type",
                PropertyName = nameof(ConsistencyViolationViewModel.RuleType),
                IsExpandable = true
            },
            new()
            {
                Type = ColumnType.Text,
                Header = "Source",
                PropertyName = nameof(ConsistencyViolationViewModel.Source),
            },
            new()
            {
                Type = ColumnType.Text,
                Header = "Target",
                PropertyName = nameof(ConsistencyViolationViewModel.Target),
            },
            new()
            {
                Type = ColumnType.Text,
                Header = "Violations",
                PropertyName = nameof(ConsistencyViolationViewModel.ViolationCount),
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