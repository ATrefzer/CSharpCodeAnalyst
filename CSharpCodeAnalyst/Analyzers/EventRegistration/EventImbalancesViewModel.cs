using System.Collections.ObjectModel;
using System.Windows;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.TabularData;

namespace CSharpCodeAnalyst.Analyzers.EventRegistration;

internal class EventImbalancesViewModel : Table
{
    private readonly ObservableCollection<TableRow> _imbalances;

    internal EventImbalancesViewModel(List<Result> imbalances)
    {
        var tmp = imbalances.Select(i => new EventImbalanceViewModel(i));
        _imbalances = new ObservableCollection<TableRow>(tmp);
    }

    public override IEnumerable<TableColumnDefinition> GetColumns()
    {
        return new List<TableColumnDefinition>
        {
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.Column_EventRegistration_Header,
                PropertyName = nameof(EventImbalanceViewModel.Description),
                IsExpandable = true
            }
        };
    }

    public override ObservableCollection<TableRow> GetData()
    {
        return _imbalances;
    }

    public override DataTemplate? GetRowDetailsTemplate()
    {
        var xamlTemplate = @"
                <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                              xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
                   <ItemsControl ItemsSource=""{Binding Locations}"">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Grid Margin=""40 0 0 0"">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width=""Auto"" />
                                        <ColumnDefinition />
                                    </Grid.ColumnDefinitions>                                  
                                    <TextBlock Grid.Column=""1"" 
                                               Text=""{Binding}"" 
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