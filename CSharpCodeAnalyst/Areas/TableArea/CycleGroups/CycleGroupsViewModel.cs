using System.Collections.ObjectModel;
using CodeParser.Analysis.Shared;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.CycleArea;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Areas.ResultArea;

internal class CycleGroupsViewModel : TableViewModel
{
    private ObservableCollection<CycleGroupViewModel> _cycleGroups = [];

    public CycleGroupsViewModel(List<CycleGroup> cycleGroups, MessageBus messaging)
    {
        Title = Strings.Tab_Summary_Cycles;
        var vms = cycleGroups.Select(g => new CycleGroupViewModel(g, messaging));
        var ordered = vms.OrderBy(g => g.Level).ThenBy(g => g.ElementCount);
        CycleGroups = new ObservableCollection<CycleGroupViewModel>(ordered);
    }

    public ObservableCollection<CycleGroupViewModel> CycleGroups
    {
        get => _cycleGroups;
        set
        {
            if (Equals(value, _cycleGroups)) return;
            _cycleGroups = value;
            OnPropertyChanged();
        }
    }

    public override void Clear()
    {
        CycleGroups.Clear();
    }
}