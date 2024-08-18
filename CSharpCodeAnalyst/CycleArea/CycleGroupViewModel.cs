using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CodeParser.Analysis.Shared;
using Contracts.Graph;

namespace CSharpCodeAnalyst.CycleArea;

internal class CycleGroupViewModel : INotifyPropertyChanged
{
    private ObservableCollection<CodeElementLineViewModel> _highLevelElements;
    private bool _isExpanded;

    public CycleGroupViewModel(CycleGroup cycleGroup)
    {
        _isExpanded = false;
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

    public int ElementCount => _highLevelElements.Count;
    public string CodeElementsDescription => $"Involves {CycleGroup.CodeGraph.Nodes.Count} code elements";

    public CycleLevel Level { get; }


    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (value == _isExpanded)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public CycleGroup CycleGroup { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

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

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}