using System.Collections.ObjectModel;
using System.Windows;
using CSharpCodeAnalyst.AnalyzerSdk.Contracts;
using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.History.Model;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Features.History;

internal class ChangeCouplingsViewModel : Table
{
    private readonly ObservableCollection<TableRow> _couplings;

    internal ChangeCouplingsViewModel(List<Coupling> couplings, IPublisher messaging)
    {
        var tmp = couplings.Select(i => new ChangeCouplingViewModel(i));
        _couplings = new ObservableCollection<TableRow>(tmp);
    }

    public override IEnumerable<TableColumnDefinition> GetColumns()
    {
        return new List<TableColumnDefinition>
        {
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.ChangeCoupling_Header_Item1,
                PropertyName = nameof(ChangeCouplingViewModel.File1)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.ChangeCoupling_Header_Item2,
                PropertyName = nameof(ChangeCouplingViewModel.File2)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.ChangeCoupling_Header_Couplings,
                PropertyName = nameof(ChangeCouplingViewModel.CouplingsText),
                SortMemberName = nameof(ChangeCouplingViewModel.Couplings)
            },
            new()
            {
                Type = ColumnType.Text,
                Header = Strings.ChangeCoupling_Header_Degree,
                PropertyName = nameof(ChangeCouplingViewModel.DegreeText),
                SortMemberName = nameof(ChangeCouplingViewModel.Degree)
            }
        };
    }

    public override ObservableCollection<TableRow> GetData()
    {
        return _couplings;
    }

    public override DataTemplate? GetRowDetailsTemplate()
    {
        return null;
    }
}