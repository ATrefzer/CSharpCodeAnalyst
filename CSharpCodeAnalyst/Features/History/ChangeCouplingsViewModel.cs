using System.Collections.ObjectModel;
using System.Windows;
using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.History.Model;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Features.History;

internal class ChangeCouplingsViewModel : Table
{
    private readonly ObservableCollection<TableRow> _couplings;

    internal ChangeCouplingsViewModel(List<Coupling> couplings)
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

    public override bool CanFilter => true;

    /// <summary>
    ///     Filters the couplings by file name. Space-separated terms are combined with AND; each
    ///     term is matched (case-insensitively) against the synthetic
    ///     <see cref="ChangeCouplingViewModel.SearchKey" />, which spans both file columns, so a
    ///     term hits whether the file sits in column 1 or column 2.
    /// </summary>
    public override ObservableCollection<TableRow> Filter(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return _couplings;
        }

        var terms = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var filtered = _couplings
            .Cast<ChangeCouplingViewModel>()
            .Where(row => terms.All(term => row.SearchKey.Contains(term, StringComparison.OrdinalIgnoreCase)));
        return new ObservableCollection<TableRow>(filtered);
    }

    public override DataTemplate? GetRowDetailsTemplate()
    {
        return null;
    }
}