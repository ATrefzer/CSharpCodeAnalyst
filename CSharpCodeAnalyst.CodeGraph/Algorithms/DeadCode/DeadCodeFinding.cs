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
    ImplementsExternalContract = 32,

    /// <summary>
    ///     Referenced, but only from test assemblies. The production code has no use for it any more, so
    ///     the test is the only thing keeping it alive - removing it means removing that test too.
    /// </summary>
    UsedOnlyByTests = 64
}

/// <summary>
///     How much the finding can be trusted. Three levels, each from one stated rule - this is a summary of
///     what we know, not a measurement.
/// </summary>
public enum DeadCodeConfidence
{
    /// <summary>
    ///     A note says the caller may sit outside the graph (entry point, test code, attributes, an
    ///     external contract). We know we might be wrong here.
    /// </summary>
    Low,

    /// <summary>
    ///     Nothing references it, but it could be reached from code we did not analyze - it is public or
    ///     protected, or the producer did not tell us its visibility.
    /// </summary>
    Medium,

    /// <summary>
    ///     Nothing references it, and nothing outside the analyzed code could: the element or one of its
    ///     containers is private or internal. "Nothing references it" and "nothing can reference it" mean
    ///     the same thing here.
    /// </summary>
    High
}

/// <summary>
///     One reported element: the topmost element of a dead subtree, plus what we know about it.
/// </summary>
public sealed class DeadCodeFinding(CodeElement element)
{
    /// <summary>The unreferenced element. Everything below it is dead too and is not reported separately.</summary>
    public CodeElement Element { get; } = element;

    /// <summary>How much the finding can be trusted - see <see cref="DeadCodeConfidence" />.</summary>
    public DeadCodeConfidence Confidence { get; init; } = DeadCodeConfidence.Medium;

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

    /// <summary>
    ///     The test elements that reference this one, set together with
    ///     <see cref="DeadCodeHint.UsedOnlyByTests" /> - what has to go with it. Can be empty although the
    ///     hint is set: liveness reaching the element through a contract member is not an edge we could
    ///     name a caller for.
    /// </summary>
    public IReadOnlyList<CodeElement> TestReferences { get; init; } = [];
}
