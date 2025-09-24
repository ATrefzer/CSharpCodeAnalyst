using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CodeParser.Analysis.EventRegistration;
using CSharpCodeAnalyst.PluginContracts;
using System.Windows.Markup;

namespace CSharpCodeAnalyst.Areas.TableArea.EventRegistration;

public class EventImbalancesViewModel : Table
{
    private ObservableCollection<TableRow> _imbalances;

    public EventImbalancesViewModel(List<EventRegistrationImbalance> imbalances)
    {
        Title = "Summary - Possible event imbalances";
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
                DisplayName = "Event handler (possible errors)",
                PropertyName = "Description",
                IsExpandable = true,
            },
        };
    }

    public override IEnumerable<TableRow> GetData()
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
                                    <!--<Image Source=""{Binding Icon}"" Margin=""0 1 5 1"" />-->
                                    <TextBlock Grid.Column=""1"" Text=""{Binding }"" Foreground=""Blue"" TextWrapping=""Wrap"">
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

        try
        {
            return (DataTemplate)XamlReader.Parse(xamlTemplate);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating row details template: {ex.Message}");
            return null;
        }
    }
}