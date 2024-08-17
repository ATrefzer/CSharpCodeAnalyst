using Contracts.Graph;

namespace CSharpCodeAnalyst.Exploration;

public interface ICodeGraphExplorer
{
    Invocation FindIncomingCalls(CodeGraph codeGraph, CodeElement method);
    Invocation FindOutgoingCalls(CodeGraph codeGraph, CodeElement method);
    Invocation FindIncomingCallsRecursive(CodeGraph codeGraph, CodeElement method);
    SearchResult FindFullInheritanceTree(CodeGraph codeGraph, CodeElement type);

    /// <summary>
    ///     Finds all dependencies connect the given nodes.
    /// </summary>
    IEnumerable<Dependency> FindAllDependencies(HashSet<string> ids, CodeGraph? graph);

    /// <summary>
    ///     Methods that implement or overload the given method
    /// </summary>
    SearchResult FindSpecializations(CodeGraph codeGraph, CodeElement method);

    /// <summary>
    ///     Methods that are implemented or overloaded by the given method
    /// </summary>
    SearchResult FindAbstractions(CodeGraph codeGraph, CodeElement method);

    SearchResult FindOutgoingDependencies(CodeGraph codeGraph, CodeElement codeElement);
    SearchResult FindIncomingDependencies(CodeGraph codeGraph, CodeElement codeElement);
}