using System.Collections.ObjectModel;
using System.Windows;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Table;

namespace CSharpCodeAnalyst.Areas.TableArea.Partitions;

public class PartitionsViewModel : Table
{
    private readonly ObservableCollection<TableRow> _partitions;

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
                Header = Strings.Partition,
                PropertyName = nameof(PartitionViewModel.PartitionName),
                IsExpandable = true
            }
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

        return CreateDataTemplateFromString(xamlTemplate);
    }
}