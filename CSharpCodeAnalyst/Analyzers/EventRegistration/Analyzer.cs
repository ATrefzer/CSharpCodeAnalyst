using Contracts.Graph;
using CSharpCodeAnalyst.Analyzers.EventRegistration.Presentation;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.UI;

namespace CSharpCodeAnalyst.Analyzers.EventRegistration;

/// <summary>
///     Finds imbalances between event registrations and un-registrations.
/// </summary>
public class Analyzer : IAnalyzer
{
    private readonly IPublisher _messaging;

    public Analyzer(IPublisher messaging)
    {
        _messaging = messaging;
    }

    public void Analyze(CodeGraph graph)
    {
        var imbalances = FindImbalances(graph);

        if (imbalances.Count == 0)
        {
            ToastManager.ShowSuccess("No event handler registration / un-registration imbalances found");
            return;
        }

        var vm = new EventImbalancesViewModel(imbalances);
        _messaging.Publish(new ShowTabularDataRequest(vm));
    }

    public string Name { get; } = Strings.Analyzer_EventRegistration_Label;
    public string Description { get; set; } = Strings.Analyzer_EventRegistration_Tooltip;

    public string Id { get; } = "EventRegistration";

    public string? GetPersistentData()
    {
        // EventRegistration analyzer has no persistent data
        return null;
    }

    public void SetPersistentData(string? data)
    {
        // EventRegistration analyzer has no persistent data
    }

    public bool IsDirty()
    {
        // This analyzer has no data.
        return false;
    }

    public event EventHandler? DataChanged;

    public static List<Result> FindImbalances(CodeGraph originalGraph)
    {
        var relationships = originalGraph.GetAllRelationships().Where(r => r.Type == RelationshipType.Handles).ToHashSet();

        var mismatches = relationships.Where(IsIncomplete);
        var imbalances = new List<Result>();

        foreach (var mismatch in mismatches)
        {
            // Assume imbalance
            var handler = originalGraph.Nodes[mismatch.SourceId];
            var target = originalGraph.Nodes[mismatch.TargetId];
            var locations = mismatch.SourceLocations;
            imbalances.Add(new Result(handler, target, locations));
        }

        return imbalances;

        bool IsIncomplete(Relationship r)
        {
            return !(r.HasAttribute(RelationshipAttribute.EventUnregistration) && r.HasAttribute(RelationshipAttribute.EventRegistration));
        }
    }
}