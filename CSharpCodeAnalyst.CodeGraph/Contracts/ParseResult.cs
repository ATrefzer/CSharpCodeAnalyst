using CSharpCodeAnalyst.CodeGraph.Declarations;
using CSharpCodeAnalyst.CodeGraph.Metrics;

namespace CSharpCodeAnalyst.CodeGraph.Contracts;

/// <summary>
///     The complete output of a parse or import: the code graph together with the (optional) per-member
///     facts collected alongside it - source metrics, and the contracts implemented from outside the
///     analyzed code. Bundling them makes it explicit that they belong to the same run and travel
///     together - there is no separate, mutable "last metrics" state on the producer.
///     Lives here rather than next to the C# parser because every graph producer returns one, and
///     the importers must not have to reference the Roslyn-based parser to do so.
///     <para>
///         <see cref="ExternalContracts" /> is optional and defaults to an empty store, so a producer that
///         knows nothing about it (every importer) stays unchanged.
///     </para>
/// </summary>
public sealed record ParseResult(Graph.CodeGraph CodeGraph, MetricStore Metrics)
{
    public ExternalContractStore ExternalContracts { get; init; } = new();
}
