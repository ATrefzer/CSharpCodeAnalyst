using System.Collections.ObjectModel;
using System.Windows;
using CodeParser.Analysis.Shared;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Messages;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst.Areas.TableArea.CycleGroups;

internal class CycleGroupsViewModel : Table
{
    private readonly ObservableCollection<TableRow> _cycleGroups;
    private readonly MessageBus _messaging;

    public CycleGroupsViewModel(List<CycleGroup> cycleGroups, MessageBus messaging)
    {
        _messaging = messaging;
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
                Command = new WpfCommand<CycleGroupViewModel>(vm =>
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
                Header = Strings.Level_Header,
                PropertyName = nameof(CycleGroupViewModel.Level)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.ElementCount_Header,
                PropertyName = nameof(CycleGroupViewModel.ElementCount)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.CodeElements_Header,
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
        var uri = new Uri(
            "/CSharpCodeAnalyst;component/Areas/TableArea/Shared/CodeElementLineTemplate.xaml",
            UriKind.Relative);
        return (DataTemplate)Application.LoadComponent(uri);
    }
}