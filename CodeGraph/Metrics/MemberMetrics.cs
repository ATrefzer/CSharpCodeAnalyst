namespace CodeGraph.Metrics;

/// <summary>
///     Source-level metrics for a single member (method, ...), collected optionally during parsing
///     and kept only for display. Purely data - no Roslyn dependency.
/// </summary>
public sealed class MemberMetrics
{
    /// <summary>Physical lines that contain actual code (excludes comment-only and blank lines).</summary>
    public int CodeLines { get; init; }

    /// <summary>
    ///     Comment-only lines, including the documentation comment above the signature. A line with
    ///     both code and a trailing comment counts as code, not as a comment line.
    /// </summary>
    public int CommentLines { get; init; }

    /// <summary>Number of executable statements (logical lines of code), block wrappers excluded.</summary>
    public int LogicalLinesOfCode { get; init; }

    /// <summary>McCabe cyclomatic complexity: 1 + number of decision points in the body.</summary>
    public int CyclomaticComplexity { get; init; }
}
