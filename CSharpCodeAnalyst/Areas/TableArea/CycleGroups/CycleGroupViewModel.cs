using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CodeParser.Analysis.Shared;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.CycleArea;
using CSharpCodeAnalyst.PluginContracts;
using Prism.Commands;

namespace CSharpCodeAnalyst.Areas.TableArea.CycleGroups;

internal class CycleGroupViewModel : TableRow
{
    private readonly MessageBus _messaging;
    private ObservableCollection<CodeElementLineViewModel> _highLevelElements;

    private void CopyToExplorerGraph(CycleGroupViewModel vm)
    {
        var graph = vm.CycleGroup.CodeGraph;

        // Send event to main view model
        _messaging.Publish(new ShowCycleGroupRequest(vm.CycleGroup));
    }

    public CycleGroupViewModel(CycleGroup cycleGroup, MessageBus messaging)
    {
        _messaging = messaging;
        CycleGroup = cycleGroup;

        CopyToExplorerGraphCommand = new DelegateCommand<CycleGroupViewModel>(CopyToExplorerGraph);


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

    public ICommand CopyToExplorerGraphCommand { get; set; }

    public string CodeElementsDescription
    {
        get => $"Involves {CycleGroup.CodeGraph.Nodes.Count} code elements";
    }

    public CycleLevel Level { get; }




    public CycleGroup CycleGroup { get; }



    private bool IsType(CodeElementType type)
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