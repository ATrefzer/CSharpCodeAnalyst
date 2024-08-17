using System.Collections.ObjectModel;
using System.ComponentModel;
using CodeParser.Analysis.Shared;
using CSharpCodeAnalyst.Common;

namespace CSharpCodeAnalyst.CycleArea;

internal class CycleSummaryViewModel : INotifyPropertyChanged
{
    private List<CycleGroup> _cycleGroups = [];
    private ObservableCollection<CycleGroupViewModel> _cycleGroupViewModels = [];


    public ObservableCollection<CycleGroupViewModel> CycleGroupViewModels
    {
        get => _cycleGroupViewModels;
        set
        {
            _cycleGroupViewModels = value;
            OnPropertyChanged(nameof(CycleGroupViewModels));
        }
    }


    public event PropertyChangedEventHandler? PropertyChanged;


    public void HandleCycleCalculationComplete(CycleCalculationComplete result)
    {
        _cycleGroups = result.CycleGroups;
        var vms = _cycleGroups.Select(g => new CycleGroupViewModel(g));
        var ordered = vms.OrderBy(g => g.Level).ThenBy(g => g.ElementCount);
        CycleGroupViewModels = new ObservableCollection<CycleGroupViewModel>(ordered);
    }


    internal void Clear()
    {
        CycleGroupViewModels.Clear();
    }


    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}