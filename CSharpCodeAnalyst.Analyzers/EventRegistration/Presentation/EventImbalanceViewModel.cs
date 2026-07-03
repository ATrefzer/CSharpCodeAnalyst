using System.Collections.ObjectModel;
using System.Windows.Input;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.Wpf;

namespace CSharpCodeAnalyst.Analyzers.EventRegistration.Presentation;

public class EventImbalanceViewModel : TableRow
{
    private readonly IPublisher _messaging;

    internal EventImbalanceViewModel(Result imbalance, IPublisher messaging)
    {
        _messaging = messaging;
        Description = imbalance.Handler.FullName;
        Locations = new ObservableCollection<SourceLocation>(imbalance.Locations);
        OpenSourceLocationCommand = new WpfCommand<SourceLocation>(OnOpenSourceLocation);
    }

    public ICommand OpenSourceLocationCommand { get; set; }

    public ObservableCollection<SourceLocation> Locations { get; set; }

    public string Description { get; }


    private void OnOpenSourceLocation(SourceLocation? location)
    {
        if (location is null)
        {
            return;
        }

        _messaging.Publish(new OpenSourceLocationRequest(location));
    }
}