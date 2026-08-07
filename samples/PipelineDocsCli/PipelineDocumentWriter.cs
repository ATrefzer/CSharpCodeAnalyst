// ============================================================================
// PIPELINE COMMENTARY 2.0 - Human-readable .pipeline writer
// ============================================================================

using System.Globalization;
using System.Text;

namespace PipelineDocsCli;

internal static class PipelineDocumentWriter
{
    public static async Task WriteAsync(PipelineSnapshot snapshot, string outputPath, CancellationToken cancellationToken)
    {
        var text = Render(snapshot);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(outputPath, text, new UTF8Encoding(false), cancellationToken);
    }

    public static string Render(PipelineSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("PIPELINE 2.0");
        builder.AppendLine();
        Line(builder, $"REPOSITORY {Path.GetFileName(snapshot.RepositoryRoot)}");
        Line(builder, $"SOLUTION {snapshot.SolutionPath}");
        Line(builder, $"GENERATED {snapshot.GeneratedAt.ToString("O", CultureInfo.InvariantCulture)}");
        builder.AppendLine();

        var solutionMembers = snapshot.Projects.Count(p => p.IsSolutionMember);
        var adjacent = snapshot.Projects.Count - solutionMembers;
        builder.AppendLine("SUMMARY");
        Line(builder, $"  projects-total: {snapshot.Projects.Count}");
        Line(builder, $"  solution-members: {solutionMembers}");
        Line(builder, $"  adjacent-projects: {adjacent}");
        Line(builder, $"  standalone-unclassified: {snapshot.Projects.Count(p => p.Classifications.Contains("standalone-unclassified"))}");
        Line(builder, $"  diagnostics: {snapshot.Diagnostics.Count}");
        builder.AppendLine("END SUMMARY");
        builder.AppendLine();

        builder.AppendLine("SOLUTION TREE");
        foreach (var project in snapshot.Projects.Where(p => p.IsSolutionMember).OrderBy(p => p.Name))
        {
            Line(builder, $"  {project.Name}");
            foreach (var reference in project.ProjectReferences) Line(builder, $"    -> {reference}");
        }
        builder.AppendLine("END SOLUTION TREE");
        builder.AppendLine();

        if (adjacent > 0)
        {
            builder.AppendLine("ADJACENT PROJECTS");
            foreach (var project in snapshot.Projects.Where(p => !p.IsSolutionMember).OrderBy(p => p.Name))
            {
                Line(builder, $"  {project.Name} [{string.Join(", ", project.Classifications)}]");
                Line(builder, $"    path: {project.Path}");
                if (project.ProjectReferences.Count > 0) Line(builder, $"    references: {string.Join(", ", project.ProjectReferences)}");
                if (project.ReferencedBy.Count > 0) Line(builder, $"    referenced-by: {string.Join(", ", project.ReferencedBy)}");
                if (!string.IsNullOrWhiteSpace(project.Purpose)) Line(builder, $"    purpose: {project.Purpose}");
            }
            builder.AppendLine("END ADJACENT PROJECTS");
            builder.AppendLine();
        }

        foreach (var project in snapshot.Projects.OrderByDescending(p => p.IsSolutionMember).ThenBy(p => p.Name))
        {
            Line(builder, $"PROJECT {project.Name}");
            Line(builder, $"  path: {project.Path}");
            Line(builder, $"  membership: {(project.IsSolutionMember ? "solution" : "adjacent")}");
            Line(builder, $"  classification: {string.Join(", ", project.Classifications)}");
            Line(builder, $"  output: {project.OutputKind}");
            Line(builder, $"  framework: {project.TargetFramework}");
            if (!string.IsNullOrWhiteSpace(project.Purpose)) Line(builder, $"  purpose: {project.Purpose}");
            WriteList(builder, "references", project.ProjectReferences);
            WriteList(builder, "referenced-by", project.ReferencedBy);

            if (project.Types.Count == 0)
            {
                builder.AppendLine("  CODE TREE unavailable");
            }
            else
            {
                builder.AppendLine("  CODE TREE");
                foreach (var type in project.Types)
                {
                    Line(builder, $"    TYPE {type.Name}");
                    Line(builder, $"      kind: {type.Kind}");
                    Line(builder, $"      file: {type.File}");
                    WriteList(builder, "implements", type.Implements, 6);
                    WriteList(builder, "calls", type.Calls, 6);
                    WriteList(builder, "called-by", type.CalledBy, 6);
                    WriteList(builder, "creates", type.Creates, 6);
                    WriteList(builder, "events", type.Events, 6);
                    WriteList(builder, "inputs", type.Inputs, 6);
                    WriteList(builder, "outputs", type.Outputs, 6);
                    builder.AppendLine("    END TYPE");
                }
                builder.AppendLine("  END CODE TREE");
            }

            builder.AppendLine("END PROJECT");
            builder.AppendLine();
        }

        if (snapshot.Diagnostics.Count > 0)
        {
            builder.AppendLine("DIAGNOSTICS");
            foreach (var diagnostic in snapshot.Diagnostics)
            {
                Line(builder, $"  [{diagnostic.Severity}] {diagnostic.Scope}: {SingleLine(diagnostic.Message)}");
            }
            builder.AppendLine("END DIAGNOSTICS");
        }

        return builder.ToString();
    }

    /// <summary>Invariant-culture lines — keep .pipeline output locale-stable (CA1305).</summary>
    private static void Line(StringBuilder builder, FormattableString message)
        => builder.AppendLine(FormattableString.Invariant(message));

    private static void WriteList(StringBuilder builder, string label, IReadOnlyList<string> values, int indent = 2)
    {
        if (values.Count == 0) return;
        var padding = new string(' ', indent);
        Line(builder, $"{padding}{label}:");
        foreach (var value in values) Line(builder, $"{padding}  - {SingleLine(value)}");
    }

    private static string SingleLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
