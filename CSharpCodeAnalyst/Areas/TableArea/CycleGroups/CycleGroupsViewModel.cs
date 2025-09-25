using System.Collections.ObjectModel;
using System.Windows;
using CodeParser.Analysis.Shared;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Messages;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Table;
using Prism.Commands;

namespace CSharpCodeAnalyst.Areas.TableArea.CycleGroups;

internal class CycleGroupsViewModel : Table
{
    private readonly ObservableCollection<TableRow> _cycleGroups;
    private readonly MessageBus _messaging;

    public CycleGroupsViewModel(List<CycleGroup> cycleGroups, MessageBus messaging)
    {
        _messaging = messaging;
        Title = Strings.Tab_Cycles;
        var vms = cycleGroups.Select(g => new CycleGroupViewModel(g));
        var ordered = vms.OrderBy(g => g.Level).ThenBy(g => g.ElementCount);
        _cycleGroups = new ObservableCollection<TableRow>(ordered);
    }


    public override List<CommandDefinition> GetCommands()
    {
        return
        [
            new CommandDefinition
            {
                Header = Strings.CopyToExplorerGraph_MenuItem,
                Command = new DelegateCommand<CycleGroupViewModel>(vm =>
                {
                    // Send event to main view model
                    _messaging.Publish(new ShowCycleGroupRequest(vm.CycleGroup));
                })
            }
        ];
    }

    public override IEnumerable<TableColumnDefinition> GetColumns()
    {
        return new List<TableColumnDefinition>
        {
            new()
            {
                Type = ColumnType.Text,
                DisplayName = Strings.Level_Header,
                PropertyName = nameof(CycleGroupViewModel.Level)
            },
            new()
            {
                Type = ColumnType.Text,
                DisplayName = Strings.ElementCount_Header,
                PropertyName = nameof(CycleGroupViewModel.ElementCount)
            },
            new()
            {
                Type = ColumnType.Text,
                DisplayName = Strings.CodeElements_Header,
                PropertyName = nameof(CycleGroupViewModel.CodeElementsDescription),
                IsExpandable = true
            }
        };
    }

    public override ObservableCollection<TableRow> GetData()
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

        return CreateDataTemplateFromString(xamlTemplate);
    }
}