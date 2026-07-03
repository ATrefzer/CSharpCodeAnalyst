using CodeGraph.Metrics;

namespace CodeParser.Parser;

/// <summary>
///     The complete output of a parse: the code graph together with the (optional) per-member source
///     metrics collected alongside it. Bundling them makes it explicit that both belong to the same
///     parse and travel together - there is no separate, mutable "last metrics" state on the parser.
/// </summary>
public sealed record ParseResult(CodeGraph.Graph.CodeGraph CodeGraph, MetricStore Metrics);
