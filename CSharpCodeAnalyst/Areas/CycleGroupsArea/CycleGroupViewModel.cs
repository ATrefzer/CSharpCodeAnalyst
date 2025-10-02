using System.Collections.ObjectModel;
using CodeParser.Analysis.Shared;
using Contracts.Graph;
using CSharpCodeAnalyst.Areas.Shared;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Areas.CycleGroupsArea;

internal class CycleGroupViewModel : TableRow
{
    private ObservableCollection<CodeElementLineViewModel> _highLevelElements;


    public CycleGroupViewModel(CycleGroup cycleGroup)
    {
        CycleGroup = cycleGroup;


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
        else if (CycleGroup.CodeGraph.Nodes.Values.Any(n => IsType(n.ElementType)))
        {
            vms = nodes.Where(n => IsType(n.ElementType))
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

    public int ElementCount
    {
        get => _highLevelElements.Count;
    }

    public string CodeElementsDescription
    {
        get => string.Format(Strings.Cycle_Groups_CodeElementsDescription, CycleGroup.CodeGraph.Nodes.Count);
    }

    public CycleLevel Level { get; }

    public CycleGroup CycleGroup { get; }

    private static bool IsType(CodeElementType type)
    {
        return type is
            CodeElementType.Class or
            CodeElementType.Interface or
            CodeElementType.Enum or
            CodeElementType.Delegate or
            CodeElementType.Struct or
            CodeElementType.Record;
    }
}