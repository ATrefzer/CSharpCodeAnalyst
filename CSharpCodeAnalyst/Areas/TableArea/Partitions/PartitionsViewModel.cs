using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;
using CSharpCodeAnalyst.PluginContracts;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Areas.TableArea.Partitions;

public class PartitionsViewModel : Table
{
    private ObservableCollection<TableRow> _partitions;

    public PartitionsViewModel(List<PartitionViewModel> pvm)
    {
        Title = Strings.Tab_Partitions;
        _partitions = new ObservableCollection<TableRow>(pvm);
    }

    public override IEnumerable<TableColumnDefinition> GetColumns()
    {
        return new List<TableColumnDefinition>
        {
            new()
            {
                Type = ColumnType.Text,
                DisplayName = "Partition",
                PropertyName = "PartitionName",
                IsExpandable = true
            },
        };
    }

    public override ObservableCollection<TableRow> GetData()
    {
        return _partitions;
    }

    public override DataTemplate? GetRowDetailsTemplate()
    {
        var xamlTemplate = @"
                <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                              xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
                    <ItemsControl ItemsSource=""{Binding CodeElements}"">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Grid Margin=""40 0 0 0"">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width=""Auto"" />
                                        <ColumnDefinition />
                                    </Grid.ColumnDefinitions>
                                    <Image Source=""{Binding Icon}"" Margin=""0 1 5 1"" />
                                    <TextBlock Grid.Column=""1"" Text=""{Binding FullName}""
                                               TextWrapping=""Wrap"" />
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