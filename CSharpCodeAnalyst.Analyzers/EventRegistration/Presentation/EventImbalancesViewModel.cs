using System.Collections.ObjectModel;
using System.Windows;
using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Analyzers.EventRegistration.Presentation;

internal class EventImbalancesViewModel : Table
{
    private readonly ObservableCollection<TableRow> _imbalances;

    internal EventImbalancesViewModel(List<Result> imbalances, IPublisher messaging)
    {
        var tmp = imbalances.Select(i => new EventImbalanceViewModel(i, messaging));
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

    public override DataTemplate GetRowDetailsTemplate()
    {
        var uri = new Uri(
            "/CSharpCodeAnalyst.Analyzers;component/EventRegistration/Presentation/SourceLocationTemplate.xaml",
            UriKind.Relative);
        return (DataTemplate)Application.LoadComponent(uri);
    }
}