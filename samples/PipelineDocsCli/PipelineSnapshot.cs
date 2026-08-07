// ============================================================================
// PIPELINE COMMENTARY 2.0 - Repository topology model
// ============================================================================

namespace PipelineDocsCli;

internal sealed record PipelineSnapshot(
    string RepositoryRoot,
    string SolutionPath,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ProjectPipeline> Projects,
    IReadOnlyList<PipelineDiagnostic> Diagnostics);

internal sealed record ProjectPipeline(
    string Name,
    string Path,
    bool IsSolutionMember,
    string OutputKind,
    string TargetFramework,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> ReferencedBy,
    IReadOnlyList<TypePipeline> Types,
    IReadOnlyList<string> Classifications,
    string? Purpose);

internal sealed record TypePipeline(
    string Name,
    string Kind,
    string File,
    IReadOnlyList<string> Implements,
    IReadOnlyList<string> Calls,
    IReadOnlyList<string> CalledBy,
    IReadOnlyList<string> Creates,
    IReadOnlyList<string> Events,
    IReadOnlyList<string> Inputs,
    IReadOnlyList<string> Outputs);

internal sealed record PipelineDiagnostic(string Severity, string Scope, string Message);
