using CodeGraph.Exploration;
using CodeGraph.Graph;

namespace CodeGraph.Contracts;

public interface ICodeGraphExplorer
{
    SearchResult FindIncomingCalls(string id);
    SearchResult FindOutgoingCalls(string id);

    /// <summary>
    ///     Follows all incoming calls recursively.
    /// </summary>
    SearchResult FindIncomingCallsRecursive(string id);

    /// <summary>
    ///     Traces back callers of the given method. Includes also abstractions and their callers
    /// </summary>
    SearchResult FollowIncomingCallsHeuristically(string id);

    SearchResult FindFullInheritanceTree(string id);

    /// <summary>
    ///     Finds all relationships connect the given nodes.
    /// </summary>
    IEnumerable<Relationship> FindAllRelationships(HashSet<string> ids);

    /// <summary>
    ///     Like <see cref="FindAllRelationships" />, but each given node is expanded to itself plus all of
    ///     its descendants first. Finds relationships between children of the given nodes even when
    ///     those children are not yet present themselves (e.g. two methods of two selected classes, or
    ///     two methods buried deep in two selected assemblies, that call each other). The result
    ///     includes the newly discovered elements plus any intermediate hierarchy (namespaces, classes)
    ///     needed to connect them to a given root.
    /// </summary>
    SearchResult FindAllRelationshipsDeep(HashSet<string> ids);

    /// <summary>
    ///     Methods that implement or overload the given method
    /// </summary>
    SearchResult FindSpecializations(string id);

    /// <summary>
    ///     Methods that are implemented or overloaded by the given method
    /// </summary>
    SearchResult FindAbstractions(string id);

    SearchResult FindOutgoingRelationships(string id);
    SearchResult FindIncomingRelationships(string id);
    void LoadCodeGraph(CodeGraph.Graph.CodeGraph graph);
    List<CodeElement> GetElements(List<string> ids);
    SearchResult FindParents(List<string> ids);

    /// <summary>
    ///     Completes the list of Ids such that at least the containing type is present.
    ///     If we already have a type the search stops.
    /// </summary>
    SearchResult FindMissingTypesForLonelyTypeMembers(HashSet<string> ids);

    SearchResult FindOutgoingRelationshipsDeep(string id);
    SearchResult FindIncomingRelationshipsDeep(string id);

    /// <summary>
    ///     Returns <paramref name="id"/> plus the ids of all PropertyAccessor children of
    ///     the element, looked up in the full code graph.
    ///     For any other element type the result contains only <paramref name="id"/> itself.
    /// </summary>
    IReadOnlyList<string> GetWithPropertyAccessors(string id);

    /// <summary>
    ///     Runs <paramref name="explore"/> for the given id. If the element is a Property,
    ///     also runs it for each PropertyAccessor child (from the full code graph) and merges
    ///     the results. For any other element type (including PropertyAccessor directly) only
    ///     the element itself is used.
    /// </summary>
    SearchResult ExploreWithAccessors(string id, Func<string, SearchResult> explore);
}