using Contracts.Graph;

namespace CSharpCodeAnalyst.Exploration;

public interface ICodeGraphExplorer
{
    Invocation FindIncomingCalls(string id);
    Invocation FindOutgoingCalls(string id);
    Invocation FindIncomingCallsRecursive(string id);
    SearchResult FindFullInheritanceTree(string id);

    /// <summary>
    ///     Finds all dependencies connect the given nodes.
    /// </summary>
    IEnumerable<Dependency> FindAllDependencies(HashSet<string> ids);

    /// <summary>
    ///     Methods that implement or overload the given method
    /// </summary>
    SearchResult FindSpecializations(string id);

    /// <summary>
    ///     Methods that are implemented or overloaded by the given method
    /// </summary>
    SearchResult FindAbstractions(string id);

    SearchResult FindOutgoingDependencies(string id);
    SearchResult FindIncomingDependencies(string id);
    void LoadCodeGraph(CodeGraph graph);
    List<CodeElement> GetElements(List<string> ids);
}