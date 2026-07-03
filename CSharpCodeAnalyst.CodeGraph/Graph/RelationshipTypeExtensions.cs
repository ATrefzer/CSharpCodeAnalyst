namespace CSharpCodeAnalyst.CodeGraph.Graph;

public static class RelationshipTypeExtensions
{
    /// <summary>
    ///     Whether a relationship expresses a real (compile-time) dependency of the source on the
    ///     target. This is the shared definition used by every analysis that reasons about
    ///     dependencies (type dependency metrics, architectural rules, ...), so they stay consistent.
    ///
    ///     Excluded:
    ///     - Containment: the parent/child hierarchy (namespace contains class, ...), not a dependency.
    ///     - Bundled: artificial edges the UI creates to fold several relationships together.
    ///     - Handles: an event-handler registration. The model stores it as handler -> event, but it
    ///       is the callback wiring (the event later calls the handler), not a dependency of the
    ///       handler on the event. The genuine "the subscriber references the event" dependency is
    ///       captured separately as a <see cref="RelationshipType.Uses" /> edge, so nothing is lost.
    /// </summary>
    public static bool IsDependency(this RelationshipType type)
    {
        return type is not (RelationshipType.Containment or RelationshipType.Bundled
            or RelationshipType.Handles);
    }
}
