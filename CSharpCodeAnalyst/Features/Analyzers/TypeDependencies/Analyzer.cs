using CodeGraph.Algorithms.Metrics;
using CSharpCodeAnalyst.Features.Analyzers.TypeDependencies.Presentation;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.UI;

namespace CSharpCodeAnalyst.Features.Analyzers.TypeDependencies;

/// <summary>
///     Describes how each type sits in the dependency structure (fan-in, fan-out, blast radius and
///     PageRank score) to surface the types worth understanding first and those risky to change.
/// </summary>
public class Analyzer : IAnalyzer
{
    private readonly IPublisher _messaging;

    public Analyzer(IPublisher messaging)
    {
        _messaging = messaging;
    }

    public string Id { get; } = "TypeDependencies";
    public string Name { get; } = Strings.Analyzer_TypeDependencies_Label;
    public string Description { get; set; } = Strings.Analyzer_TypeDependencies_Tooltip;

    public void Analyze(CodeGraph.Graph.CodeGraph graph)
    {
        var results = TypeDependencyAnalysis.Calculate(graph);

        if (results.Count == 0)
        {
            ToastManager.ShowInfo(Strings.Analyzer_TypeDependencies_NoData);
            return;
        }

        var vm = new TypeDependenciesViewModel(results, _messaging);
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
