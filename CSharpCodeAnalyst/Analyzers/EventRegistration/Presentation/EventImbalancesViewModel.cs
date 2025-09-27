using System.Collections.ObjectModel;
using System.Windows;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.TabularData;

namespace CSharpCodeAnalyst.Analyzers.EventRegistration.Presentation;

internal class EventImbalancesViewModel : Table
{
    private readonly ObservableCollection<TableRow> _imbalances;

    internal EventImbalancesViewModel(List<Result> imbalances)
    {
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

    public override DataTemplate? GetRowDetailsTemplate()
    {
        var uri = new Uri(
            "/CSharpCodeAnalyst;component/Analyzers/EventRegistration/Presentation/SourceLocationTemplate.xaml",
            UriKind.Relative);
        return (DataTemplate)Application.LoadComponent(uri);
    }
}