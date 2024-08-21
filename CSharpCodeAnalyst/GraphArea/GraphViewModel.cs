using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Exploration;
using CSharpCodeAnalyst.GraphArea.RenderOptions;
using CSharpCodeAnalyst.Help;
using Prism.Commands;

namespace CSharpCodeAnalyst.GraphArea;

internal class GraphViewModel : INotifyPropertyChanged
{
    private readonly ICodeGraphExplorer _explorer;

    private readonly IDependencyGraphViewer _viewer;

    private CodeGraph? _originalCodeGraph;
    private HighlightOption _selectedHighlightOption;
    private RenderOption _selectedRenderOption;
    private bool _showFlatGraph;

    internal GraphViewModel(IDependencyGraphViewer viewer, ICodeGraphExplorer explorer)
    {
        _viewer = viewer;
        _explorer = explorer;

        // Initialize RenderOptions
        RenderOptions = new ObservableCollection<RenderOption>
        {
            new DefaultRenderOptions(),
            new LeftToRightRenderOptions(),
            new BottomToTopRenderOptions()
        };

        HighlightOptions = new ObservableCollection<HighlightOption>
        {
            HighlightOption.Default,
            new(HighlightMode.OutgoingEdgesChildrenAndSelf, "Outgoing edges")
        };

        // Set defaults
        _selectedRenderOption = RenderOptions[0];
        _selectedHighlightOption = HighlightOptions[0];

        var findOutgoingCalls = "Find outgoing Calls";
        var findIncomingCalls = "Find incoming Calls";
        var findIncomingCallsRecursive = "Find incoming Calls (recursive)";
        var findSpecializations = "Find specializations";
        var findAbstractions = "Find abstractions";


        // Methods
        _viewer.AddContextCommand(new ContextCommand(findOutgoingCalls, CodeElementType.Method, FindOutgoingCalls));
        _viewer.AddContextCommand(new ContextCommand(findIncomingCalls, CodeElementType.Method, FindIncomingCalls));
        _viewer.AddContextCommand(new ContextCommand(findIncomingCallsRecursive, CodeElementType.Method,
            FindIncomingCallsRecursive));
        _viewer.AddContextCommand(new ContextCommand(findSpecializations, CodeElementType.Method, FindSpecializations));
        _viewer.AddContextCommand(new ContextCommand(findAbstractions, CodeElementType.Method, FindAbstractions));


        // Properties
        _viewer.AddContextCommand(new ContextCommand(findOutgoingCalls, CodeElementType.Property, FindOutgoingCalls));
        _viewer.AddContextCommand(new ContextCommand(findIncomingCalls, CodeElementType.Property, FindIncomingCalls));
        _viewer.AddContextCommand(new ContextCommand(findIncomingCallsRecursive, CodeElementType.Property,
            FindIncomingCallsRecursive));
        _viewer.AddContextCommand(
            new ContextCommand(findSpecializations, CodeElementType.Property, FindSpecializations));
        _viewer.AddContextCommand(new ContextCommand(findAbstractions, CodeElementType.Property, FindAbstractions));


        // Classes
        _viewer.AddContextCommand(new ContextCommand("Find full inheritance tree", CodeElementType.Class,
            FindSpecializationAndAbstractions));
        _viewer.AddContextCommand(new ContextCommand(findSpecializations, CodeElementType.Class, FindSpecializations));
        _viewer.AddContextCommand(new ContextCommand(findAbstractions, CodeElementType.Class, FindAbstractions));

        // Interfaces
        _viewer.AddContextCommand(new ContextCommand("Find full inheritance tree", CodeElementType.Interface,
            FindSpecializationAndAbstractions));
        _viewer.AddContextCommand(new ContextCommand(findSpecializations, CodeElementType.Interface,
            FindSpecializations));
        _viewer.AddContextCommand(new ContextCommand(findAbstractions, CodeElementType.Interface, FindAbstractions));


        // Everyone gets the in/out dependencies
        _viewer.AddContextCommand(new SeparatorCommand());
        foreach (var type in Enum.GetValues<CodeElementType>())
        {
            _viewer.AddContextCommand(new ContextCommand("All incoming dependencies", type,
                FindAllIncomingDependencies));
            _viewer.AddContextCommand(new ContextCommand("All outgoing dependencies", type,
                FindAllOutgoingDependencies));
        }

        UndoCommand = new DelegateCommand(Undo);
    }

    public ObservableCollection<HighlightOption> HighlightOptions { get; }


    public ObservableCollection<RenderOption> RenderOptions { get; }

    public RenderOption SelectedRenderOption
    {
        get => _selectedRenderOption;
        set
        {
            if (_selectedRenderOption != value)
            {
                _selectedRenderOption = value;
                OnPropertyChanged(nameof(SelectedRenderOption));
                UpdateGraphRenderOption();
            }
        }
    }


    public bool ShowFlatGraph
    {
        get => _showFlatGraph;
        set
        {
            if (value == _showFlatGraph)
            {
                return;
            }

            _showFlatGraph = value;
            _viewer.ShowFlatGraph(value);
            OnPropertyChanged(nameof(ShowFlatGraph));
        }
    }

    public HighlightOption SelectedHighlightOption
    {
        get => _selectedHighlightOption;
        set
        {
            if (_selectedHighlightOption == value)
            {
                return;
            }

            _selectedHighlightOption = value;
            _viewer.SetHighlightMode(value.Mode);
            OnPropertyChanged(nameof(SelectedHighlightOption));
        }
    }

    public ICommand UndoCommand { get; }


    public event PropertyChangedEventHandler? PropertyChanged;


    private void Undo()
    {
        if (!_viewer.Undo())
        {
            MessageBox.Show("Nothing to undo!", "", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void UpdateGraphRenderOption()
    {
        _viewer.UpdateRenderOption(SelectedRenderOption);
    }

    private void FindAllOutgoingDependencies(CodeElement element)
    {
        if (_originalCodeGraph is null)
        {
            return;
        }

        var result = _explorer.FindOutgoingDependencies(_originalCodeGraph!, _originalCodeGraph.Nodes[element.Id]);
        _viewer.AddToGraph(result.Elements, result.Dependencies);
    }

    private void FindAllIncomingDependencies(CodeElement element)
    {
        if (_originalCodeGraph is null)
        {
            return;
        }

        var result = _explorer.FindIncomingDependencies(_originalCodeGraph!, _originalCodeGraph.Nodes[element.Id]);
        _viewer.AddToGraph(result.Elements, result.Dependencies);
    }

    private void FindSpecializations(CodeElement method)
    {
        if (_originalCodeGraph is null)
        {
            return;
        }

        var result = _explorer.FindSpecializations(_originalCodeGraph!, _originalCodeGraph.Nodes[method.Id]);
        _viewer.AddToGraph(result.Elements, result.Dependencies);
    }

    private void FindAbstractions(CodeElement method)
    {
        if (_originalCodeGraph is null)
        {
            return;
        }

        var result = _explorer.FindAbstractions(_originalCodeGraph!, _originalCodeGraph.Nodes[method.Id]);
        _viewer.AddToGraph(result.Elements, result.Dependencies);
    }

    public void LoadCodeGraph(CodeGraph codeGraph)
    {
        _originalCodeGraph = codeGraph;
        Clear();

        // Only update after we change the code graph.
        _viewer.SetQuickInfoFactory(new QuickInfoFactory(_originalCodeGraph));
    }

    internal void AddToGraph(IEnumerable<CodeElement> nodes, IEnumerable<Dependency> dependencies)
    {
        _viewer.AddToGraph(nodes, dependencies);
    }

    internal void HandleAddNodeToGraphRequest(AddNodeToGraphRequest request)
    {
        AddToGraph(new List<CodeElement> { request.Node }, []);
    }

    internal void Clear()
    {
        _viewer.Clear();
    }

    internal void Layout()
    {
        _viewer.Layout();
    }

    internal void FindIncomingCalls(CodeElement method)
    {
        if (_originalCodeGraph is null || !IsMethodOrProperty(method))
        {
            return;
        }

        // Use the node from the original graph
        var callee = _explorer.FindIncomingCalls(_originalCodeGraph!, _originalCodeGraph.Nodes[method.Id]);
        AddToGraph(callee.Methods, callee.Calls);
    }

    internal void FindIncomingCallsRecursive(CodeElement method)
    {
        if (_originalCodeGraph is null || !IsMethodOrProperty(method))
        {
            return;
        }

        var callers =
            _explorer.FindIncomingCallsRecursive(_originalCodeGraph!, _originalCodeGraph.Nodes[method.Id]);
        AddToGraph(callers.Methods, callers.Calls);
    }

    internal void FindSpecializationAndAbstractions(CodeElement? type)
    {
        if (_originalCodeGraph is null || type is null)
        {
            return;
        }

        var relationships =
            _explorer.FindFullInheritanceTree(_originalCodeGraph!, _originalCodeGraph.Nodes[type.Id]);
        AddToGraph(relationships.Elements, relationships.Dependencies);
    }

    internal void FindOutgoingCalls(CodeElement? method)
    {
        if (_originalCodeGraph is null || method is null || !IsMethodOrProperty(method))
        {
            return;
        }

        var callers = _explorer.FindOutgoingCalls(_originalCodeGraph!, _originalCodeGraph.Nodes[method.Id]);
        AddToGraph(callers.Methods, callers.Calls);
    }

    private static bool IsMethodOrProperty(CodeElement? method)
    {
        return method is { ElementType: CodeElementType.Method or CodeElementType.Property };
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    ///     Note that dependency type "Contains" is treated special
    /// </summary>
    public CodeGraph ExportGraph()
    {
        return _viewer.GetStructure();
    }

    public void HandleAddMissingDependenciesRequest(AddMissingDependenciesRequest obj)
    {
        var viewerGraph = _viewer.GetStructure();
        var ids = viewerGraph.Nodes.Keys.ToHashSet();
        var dependencies = _explorer.FindAllDependencies(ids, _originalCodeGraph);
        _viewer.AddToGraph([], dependencies);
    }

    public void SaveToSvg(FileStream stream)
    {
        _viewer.SaveToSvg(stream);
    }

    public void ShowGlobalContextMenu()
    {
        _viewer.ShowGlobalContextMenu();
    }

    public void ImportCycleGroup(List<CodeElement> codeElements, List<Dependency> dependencies)
    {
        _viewer.ImportCycleGroup(codeElements, dependencies);
    }
}