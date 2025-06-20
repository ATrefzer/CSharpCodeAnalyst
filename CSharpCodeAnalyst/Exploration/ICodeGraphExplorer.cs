using Contracts.Graph;

namespace CSharpCodeAnalyst.Exploration;

public interface ICodeGraphExplorer
{
    Invocation FindIncomingCalls(string id);
    Invocation FindOutgoingCalls(string id);

    /// <summary>
    ///     Follows all incoming calls recursively.
    /// </summary>
    Invocation FindIncomingCallsRecursive(string id);

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
    ///     Methods that implement or overload the given method
    /// </summary>
    SearchResult FindSpecializations(string id);

    /// <summary>
    ///     Methods that are implemented or overloaded by the given method
    /// </summary>
    SearchResult FindAbstractions(string id);

    SearchResult FindOutgoingRelationships(string id);
    SearchResult FindIncomingRelationships(string id);
    void LoadCodeGraph(CodeGraph graph);
    List<CodeElement> GetElements(List<string> ids);
    SearchResult FindParents(List<string> ids);

    /// <summary>
    ///     Completes the list of Ids such that at least the containing type is present.
    ///     If we already have a type the search stops.
    /// </summary>
    SearchResult CompleteToContainingTypes(HashSet<string> ids);
}