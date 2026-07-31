using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Algorithms.DeadCode;

/// <summary>
///     Reasons why a reported element may nevertheless be alive. The analysis works on the relationships
///     the parser could see, so everything reached through XAML, reflection, dependency injection or
///     serialization looks unreferenced. Rather than silently dropping such elements, they are reported
///     with the hint that explains why they are suspicious - the caller decides what to do with them.
/// </summary>
[Flags]
public enum DeadCodeHint
{
    None = 0,

    /// <summary>Called from outside the analyzed code by definition (program entry point).</summary>
    EntryPoint = 1,

    /// <summary>The element or something in its subtree carries a known test-framework attribute.</summary>
    TestCode = 2,

    /// <summary>Carries attributes. Attributes often mean an external framework drives the element.</summary>
    Attributed = 4,

    /// <summary>
    ///     A contract member (interface or base member) that is implemented but never called through the
    ///     contract - the abstraction itself is unused.
    /// </summary>
    ContractNeverCalled = 8,

    /// <summary>
    ///     Implements or overrides an internal contract member that is itself dead, so it can only be
    ///     removed together with that contract.
    /// </summary>
    ImplementsDeadContract = 16,

    /// <summary>
    ///     Implements or overrides a contract from outside the analyzed code (a framework interface, a
    ///     base member from a referenced assembly). The caller is the framework, so nothing in the graph
    ///     references it - it is almost certainly alive.
    /// </summary>
    ImplementsExternalContract = 32
}

/// <summary>
///     One reported element: the topmost element of a dead subtree, plus what we know about it.
/// </summary>
public sealed class DeadCodeFinding(CodeElement element)
{
    /// <summary>The unreferenced element. Everything below it is dead too and is not reported separately.</summary>
    public CodeElement Element { get; } = element;

    public DeadCodeHint Hints { get; init; }

    /// <summary>Distinct attribute names found on the element and its subtree.</summary>
    public IReadOnlyList<string> Attributes { get; init; } = [];

    /// <summary>
    ///     The contract outside the analyzed code this element implements or overrides, e.g.
    ///     "IDisposable.Dispose". Set together with <see cref="DeadCodeHint.ImplementsExternalContract" />.
    /// </summary>
    public string? ExternalContract { get; init; }

    /// <summary>
    ///     The polymorphically related members: the internal contract members this element implements
    ///     (<see cref="DeadCodeHint.ImplementsDeadContract" />) and the implementations that die with it
    ///     (<see cref="DeadCodeHint.ContractNeverCalled" />).
    /// </summary>
    public IReadOnlyList<CodeElement> RelatedMembers { get; init; } = [];
}
