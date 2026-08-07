namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Sarif;

/// <summary>
///     Everything the SARIF output needs about the run itself. Kept out of
///     <see cref="SarifFormatter" /> so the formatter touches neither the file system nor the running
///     assembly and can be tested from a plain in-memory graph.
/// </summary>
public sealed class SarifContext
{
    /// <summary>
    ///     Directory the file URIs are made relative to - the repository root. Defaults to the
    ///     directory of the solution, which is only the same thing when the solution sits at the root.
    /// </summary>
    public string? SourceRoot { get; init; }

    /// <summary>The rules file, used to point a finding back at the rule that produced it.</summary>
    public string? RulesFile { get; init; }

    /// <summary>
    ///     The version of the tool as the build stamped it, free form - "0.9.0+abc1234" and the
    ///     four-part "0.1.0.0" of a build without an explicit version are both fine. Whether a SARIF
    ///     <c>semanticVersion</c> can be derived from it is decided in <see cref="SarifFormatter" />.
    /// </summary>
    public string ToolVersion { get; init; } = "0.0.0";

    /// <summary>
    ///     Problems of the run itself, not findings about the code - most importantly parser failures.
    ///     A consumer that only ever reads the SARIF file must still be able to see that the graph the
    ///     rules ran against was incomplete.
    /// </summary>
    public IReadOnlyList<string> RunNotifications { get; init; } = [];
}
