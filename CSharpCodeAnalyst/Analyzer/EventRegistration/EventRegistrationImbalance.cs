using Contracts.Graph;

namespace CSharpCodeAnalyst.Analyzer.EventRegistration;

/// <summary>
///     Finds imbalances between event registrations and un-registrations.
/// </summary>
public class EventRegistrationImbalance
{
    public EventRegistrationImbalance(CodeElement handler, CodeElement evt, List<SourceLocation> locations)
    {
        Handler = handler;
        Event = evt;
        Locations = locations;
    }

    public CodeElement Handler { get; }
    public CodeElement Event { get; }
    public List<SourceLocation> Locations { get; }
}