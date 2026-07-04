namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

/// <summary>
///     Result of a rule analysis: the violations found plus warnings about rules
///     that could not take effect (e.g. patterns not matching any code element).
/// </summary>
public class RuleAnalysisResult
{
    public List<Violation> Violations { get; } = [];

    public List<string> Warnings { get; } = [];
}
