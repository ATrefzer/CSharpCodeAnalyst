using System.Collections.ObjectModel;
using CodeParser.Analysis.EventRegistration;
using CSharpCodeAnalyst.Resources;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.Areas.ResultArea;

public class EventImbalancesViewModel : TableViewModel
{
    private ObservableCollection<EventImbalanceViewModel> _imbalances = [];

    public EventImbalancesViewModel(List<EventRegistrationImbalance> imbalances)
    {
        Title = "Summary - Possible event imbalances";
        var tmp = imbalances.Select(i => new EventImbalanceViewModel(i));
        Imbalances = new ObservableCollection<EventImbalanceViewModel>(tmp);
    }

    public ObservableCollection<EventImbalanceViewModel> Imbalances
    {
        get => _imbalances;
        set
        {
            if (Equals(value, _imbalances)) return;
            _imbalances = value;
            OnPropertyChanged();
        }
    }

    public override void Clear()
    {
        Imbalances.Clear();
    }

}