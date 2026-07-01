using CodeGraph.Algorithms.Metrics;
using CSharpCodeAnalyst.Features.Analyzers.Hotspots.Presentation;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.UI;

namespace CSharpCodeAnalyst.Features.Analyzers.Hotspots;

/// <summary>
///     Ranks the types of the code graph by their centrality in the dependency structure
///     (fan-in, fan-out and PageRank) to surface the types worth understanding first.
/// </summary>
public class Analyzer : IAnalyzer
{
    private readonly IPublisher _messaging;

    public Analyzer(IPublisher messaging)
    {
        _messaging = messaging;
    }

    public string Id { get; } = "DependencyHotspots";
    public string Name { get; } = Strings.Analyzer_Hotspots_Label;
    public string Description { get; set; } = Strings.Analyzer_Hotspots_Tooltip;

    public void Analyze(CodeGraph.Graph.CodeGraph graph)
    {
        var hotspots = HotspotAnalysis.Calculate(graph);

        if (hotspots.Count == 0)
        {
            ToastManager.ShowInfo(Strings.Analyzer_Hotspots_NoData);
            return;
        }

        var vm = new HotspotsViewModel(hotspots, _messaging);
        _messaging.Publish(new ShowTabularDataRequest(vm));
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
