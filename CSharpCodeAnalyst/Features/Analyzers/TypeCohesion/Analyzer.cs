using CodeGraph.Algorithms.Partitioning;
using CSharpCodeAnalyst.Features.Analyzers.TypeCohesion.Presentation;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.UI;

namespace CSharpCodeAnalyst.Features.Analyzers.TypeCohesion;

/// <summary>
///     Flags classes whose members fall into several independent groups (partitions) and are
///     therefore candidates for splitting. Each row can be drilled into the partition view.
/// </summary>
public class Analyzer : IAnalyzer
{
    private readonly IPublisher _messaging;

    public Analyzer(IPublisher messaging)
    {
        _messaging = messaging;
    }

    public string Id { get; } = "TypeCohesion";
    public string Name { get; } = Strings.Analyzer_TypeCohesion_Label;
    public string Description { get; set; } = Strings.Analyzer_TypeCohesion_Tooltip;

    public void Analyze(CodeGraph.Graph.CodeGraph graph)
    {
        var results = TypeCohesionAnalysis.Calculate(graph);

        if (results.Count == 0)
        {
            ToastManager.ShowInfo(Strings.Analyzer_TypeCohesion_NoData);
            return;
        }

        var vm = new TypeCohesionViewModel(results, _messaging);
        _messaging.Publish(new ShowTabularDataRequest(Id, Name, vm));
    }

    public string? GetPersistentData()
    {
        // No configuration or state to persist.
        return null;
    }

    public void SetPersistentData(string? data)
    {
        // No configuration or state to persist.
    }

    public bool IsDirty()
    {
        return false;
    }

    public event EventHandler? DataChanged;

    protected virtual void OnDataChanged()
    {
        DataChanged?.Invoke(this, EventArgs.Empty);
    }
}
