namespace CodeGraph.Metrics;

/// <summary>
///     Source-level metrics for a single member (method, ...), collected optionally during parsing
///     and kept only for display. Purely data - no Roslyn dependency.
/// </summary>
public sealed class MemberMetrics
{
    /// <summary>Physical lines the declaration spans (last source line - first source line + 1).</summary>
    public int LinesOfCode { get; init; }

    /// <summary>McCabe cyclomatic complexity: 1 + number of decision points in the body.</summary>
    public int CyclomaticComplexity { get; init; }
}
