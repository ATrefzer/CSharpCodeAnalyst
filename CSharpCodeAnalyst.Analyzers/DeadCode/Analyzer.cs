using CSharpCodeAnalyst.Analyzers.DeadCode.Presentation;
using CSharpCodeAnalyst.Analyzers.Resources;
using CSharpCodeAnalyst.AnalyzerSdk.Contracts;
using CSharpCodeAnalyst.AnalyzerSdk.Messages;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.CodeGraph.Algorithms.DeadCode;

namespace CSharpCodeAnalyst.Analyzers.DeadCode;

/// <summary>
///     Lists the code nobody references any more - the topmost element of every dead subtree, together
///     with the hint that explains why it might still be alive.
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

    public string Id { get; } = "DeadCode";
    public string Name { get; } = Strings.Analyzer_DeadCode_Label;
    public string Description { get; set; } = Strings.Analyzer_DeadCode_Tooltip;

    public void Analyze(CodeGraph.Graph.CodeGraph graph)
    {
        var findings = DeadCodeAnalysis.Calculate(graph);

        if (findings.Count == 0)
        {
            _userNotification.ShowSuccess(Strings.Analyzer_DeadCode_NoData);
            return;
        }

        var vm = new DeadCodeViewModel(findings, _messaging);
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
