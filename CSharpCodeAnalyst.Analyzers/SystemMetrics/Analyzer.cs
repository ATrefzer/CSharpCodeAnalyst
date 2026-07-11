using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.Analyzers.SystemMetrics.Presentation;
using CSharpCodeAnalyst.AnalyzerSdk.Contracts;
using CSharpCodeAnalyst.AnalyzerSdk.Messages;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;

namespace CSharpCodeAnalyst.Analyzers.SystemMetrics;

/// <summary>
///     Computes metrics about the code base as a whole (e.g. propagation cost) rather than per type.
/// </summary>
public class Analyzer : IAnalyzer
{
    private readonly IPublisher _messaging;
    private readonly IUserNotification _userNotification;

    public Analyzer(IPublisher messaging, IUserNotification userNotification)
    {
        _messaging = messaging;
        _userNotification = userNotification;
    }

    public string Id { get; } = "SystemMetrics";
    public string Name { get; } = Strings.Analyzer_SystemMetrics_Label;
    public string Description { get; } = Strings.Analyzer_SystemMetrics_Tooltip;

    public void Analyze(CodeGraph.Graph.CodeGraph graph)
    {
        var metrics = SystemMetricsAnalysis.Calculate(graph);

        if (metrics.TypeCount < 2)
        {
            _userNotification.ShowSuccess(Strings.Analyzer_SystemMetrics_NoData);
            return;
        }

        var vm = new SystemMetricsViewModel(metrics);
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
