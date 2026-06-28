namespace CSharpCodeAnalyst.Shared.Messages;

/// <summary>
///     Which exploration to run on the currently selected elements. Mirrors the
///     per-element context-menu actions, but applied to the whole selection at once.
/// </summary>
public enum ExploreDirection
{
    OutgoingRelationships,
    IncomingRelationships,
    OutgoingRelationshipsDeep,
    IncomingRelationshipsDeep
}

/// <summary>
///     Asks the graph view model to expand the current selection by following relationships
///     in the given direction. Raised from the keyboard shortcuts in the web graph
///     (Arrow Up/Down = outgoing/incoming, Page Up/Down = the deep variants).
/// </summary>
public sealed record ExploreSelectedRequest(ExploreDirection Direction);
