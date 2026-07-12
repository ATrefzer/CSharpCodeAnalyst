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

        // Level and element list come from the SCC vertices, the actual cycle participants.
        // The detailed graph is the wrong source here: it additionally contains the concrete
        // relationship endpoints and gap-filling containers (e.g. a nested namespace an endpoint
        // lives in), which inflated the count - and it is what the NOCYCLES rule violations
        // report, so both views now show the same number.
        var vertices = cycleGroup.Vertices;
        if (vertices.Any(v => v.ElementType == CodeElementType.Assembly))
        {
            Level = CycleLevel.Assembly;
        }
        else if (vertices.Any(v => v.ElementType == CodeElementType.Namespace))
        {
            Level = CycleLevel.Namespace;
        }
        else if (vertices.Any(v => v.IsType()))
        {
            Level = CycleLevel.Type;
        }
        else
        {
            Level = CycleLevel.Other;
        }

        var vms = vertices.Select(e => new CodeElementLineViewModel(e)).ToList();
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