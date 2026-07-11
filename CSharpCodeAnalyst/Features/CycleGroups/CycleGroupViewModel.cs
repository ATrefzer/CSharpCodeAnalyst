using System.Collections.ObjectModel;
using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.CodeGraph.Algorithms.Cycles;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.UI;

namespace CSharpCodeAnalyst.Features.CycleGroups;

internal class CycleGroupViewModel : TableRow
{
    private ObservableCollection<CodeElementLineViewModel> _highLevelElements;
    private string _name;


    public CycleGroupViewModel(CycleGroup cycleGroup)
    {
        CycleGroup = cycleGroup;

        _name = cycleGroup.Name;

        var nodes = CycleGroup.CodeGraph.Nodes.Values;
        List<CodeElementLineViewModel> vms;
        if (nodes.Any(c => c.ElementType == CodeElementType.Assembly))
        {
            vms = nodes.Where(n => n.ElementType is CodeElementType.Assembly)
                .Select(e => new CodeElementLineViewModel(e)).ToList();
            Level = CycleLevel.Assembly;
        }
        else if (CycleGroup.CodeGraph.Nodes.Values.Any(c => c.ElementType == CodeElementType.Namespace))
        {
            vms = nodes.Where(n => n.ElementType is CodeElementType.Namespace)
                .Select(e => new CodeElementLineViewModel(e)).ToList();
            Level = CycleLevel.Namespace;
        }
        else if (CycleGroup.CodeGraph.Nodes.Values.Any(n => n.IsType()))
        {
            vms = nodes.Where(n => n.IsType())
                .Select(e => new CodeElementLineViewModel(e)).ToList();
            Level = CycleLevel.Type;
        }
        else
        {
            vms = nodes.Select(e => new CodeElementLineViewModel(e)).ToList();
            Level = CycleLevel.Other;
        }

        vms.Sort(new Sorter());

        _highLevelElements = new ObservableCollection<CodeElementLineViewModel>(vms);
    }

    public string Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            _name = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<CodeElementLineViewModel> CodeElements
    {
        get => _highLevelElements;
        set
        {
            if (Equals(value, _highLevelElements))
            {
                return;
            }

            _highLevelElements = value;
            OnPropertyChanged();
        }
    }

    public int HighLevelElementCount
    {
        get => _highLevelElements.Count;
    }

    public int InvolvedCodeElementsCount
    {
        get => CycleGroup.CodeGraph.Nodes.Count;
    }

    public string CodeElementsDescription
    {
        get => string.Format(Strings.Cycle_Groups_CodeElementsDescription, InvolvedCodeElementsCount);
    }

    public CycleLevel Level { get; }

    public CycleGroup CycleGroup { get; }
}