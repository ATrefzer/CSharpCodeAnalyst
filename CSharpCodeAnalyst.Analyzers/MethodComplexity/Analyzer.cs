using CodeGraph.Metrics;
using CSharpCodeAnalyst.Analyzers.MethodComplexity.Presentation;
using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.Notifications;

namespace CSharpCodeAnalyst.Analyzers.MethodComplexity;

/// <summary>
///     Lists per-method source metrics (lines of code, cyclomatic complexity) collected during
///     import. Reads the shared <see cref="MetricStore" />; empty unless metric collection was
///     enabled in the settings before importing.
/// </summary>
public class Analyzer : IAnalyzer
{
    private readonly MetricStore _metricStore;
    private readonly IPublisher _messaging;
    private readonly IUserNotification _userNotification;

    public Analyzer(IPublisher messaging, IUserNotification userNotification, MetricStore metricStore)
    {
        _messaging = messaging;
        _userNotification = userNotification;
        _metricStore = metricStore;
    }

    public string Id { get; } = "MethodComplexity";
    public string Name { get; } = Strings.Analyzer_MethodComplexity_Label;
    public string Description { get; set; } = Strings.Analyzer_MethodComplexity_Tooltip;

    public void Analyze(CodeGraph.Graph.CodeGraph graph)
    {
        if (_metricStore.IsEmpty)
        {
            _userNotification.ShowSuccess(Strings.Analyzer_MethodComplexity_NoData);
            return;
        }

        var rows = _metricStore.Metrics
            .Where(kvp => graph.Nodes.ContainsKey(kvp.Key))
            .Select(kvp => new MethodComplexityRowViewModel(graph.Nodes[kvp.Key], kvp.Value))
            .OrderByDescending(r => r.Complexity)
            .ThenByDescending(r => r.Code)
            .ThenBy(r => r.Name)
            .ToList();

        if (rows.Count == 0)
        {
            _userNotification.ShowSuccess(Strings.Analyzer_MethodComplexity_NoData);
            return;
        }

        var vm = new MethodComplexityViewModel(rows, _messaging);
        _messaging.Publish(new ShowTabularDataRequest(Id, Name, vm));
    }

    public string? GetPersistentData()
    {
        return null;
    }

    public void SetPersistentData(string? data)
    {
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
