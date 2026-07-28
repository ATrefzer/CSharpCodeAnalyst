using CSharpCodeAnalyst.CodeGraph.Metrics;

namespace CSharpCodeAnalyst.CodeGraph.Contracts;

/// <summary>
///     The complete output of a parse or import: the code graph together with the (optional)
///     per-member source metrics collected alongside it. Bundling them makes it explicit that both
///     belong to the same run and travel together - there is no separate, mutable "last metrics"
///     state on the producer.
///     Lives here rather than next to the C# parser because every graph producer returns one, and
///     the importers must not have to reference the Roslyn-based parser to do so.
/// </summary>
public sealed record ParseResult(Graph.CodeGraph CodeGraph, MetricStore Metrics);
