using System.Collections.ObjectModel;
using System.Windows;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Areas.TableArea.Partitions;

public class PartitionsViewModel : Table
{
    private readonly ObservableCollection<TableRow> _partitions;

    public PartitionsViewModel(List<PartitionViewModel> pvm)
    {
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
        var uri = new Uri(
            "/CSharpCodeAnalyst;component/Areas/TableArea/Shared/CodeElementLineTemplate.xaml",
            UriKind.Relative);
        return (DataTemplate)Application.LoadComponent(uri);
    }
}