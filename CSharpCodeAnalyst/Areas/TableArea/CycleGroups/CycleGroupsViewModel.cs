using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;
using CodeParser.Analysis.Shared;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.PluginContracts;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Areas.TableArea.CycleGroups;

internal class CycleGroupsViewModel : Table
{
    private readonly ObservableCollection<CycleGroupViewModel> _cycleGroups;

    public CycleGroupsViewModel(List<CycleGroup> cycleGroups, MessageBus messaging)
    {
        Title = Strings.Tab_Summary_Cycles;
        var vms = cycleGroups.Select(g => new CycleGroupViewModel(g, messaging));
        var ordered = vms.OrderBy(g => g.Level).ThenBy(g => g.ElementCount);
        _cycleGroups = new ObservableCollection<CycleGroupViewModel>(ordered);
    }


    public override IEnumerable<TableColumnDefinition> GetColumns()
    {
        return new List<TableColumnDefinition>
        {
            new()
            {
                Type = ColumnType.Text,
                DisplayName = "Level",
                PropertyName = "Level"
            },
            new()
            {
                Type = ColumnType.Text,
                DisplayName = "ElementCount",
                PropertyName = "ElementCount"
            },
            new()
            {
                Type = ColumnType.Text,
                DisplayName = "CodeElementsDescription",
                PropertyName = "CodeElementsDescription",
                IsExpandable = true
            }
        };
    }

    public override IEnumerable<TableRow> GetData()
    {
        return _cycleGroups;
    }

    public override DataTemplate? GetRowDetailsTemplate()
    {
        var xamlTemplate = @"
                <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                              xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
                <ItemsControl ItemsSource=""{Binding CodeElements}"">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Grid Margin=""20 0 0 0"">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width=""Auto"" />
                                        <ColumnDefinition />
                                    </Grid.ColumnDefinitions>
                                    <Image Source=""{Binding Icon}"" Margin=""0 1 5 1"" />
                                    <TextBlock Grid.Column=""1"" Text=""{Binding FullName}"" TextWrapping=""Wrap"" />
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