using System.Text;
using CSharpCodeAnalyst.Analyzers.Resources;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

/// <summary>
/// Outputs the analysis result for the CLI interface.
/// </summary>
public static class ViolationsFormatter
{
    public static string Format(CodeGraph.Graph.CodeGraph graph, RuleAnalysisResult result)
    {
        var sb = new StringBuilder();

        if (result.Warnings.Count > 0)
        {
            sb.AppendLine(Strings.Cmd_WarningsHeader);
            foreach (var warning in result.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }

            sb.AppendLine();
        }

        var violations = result.Violations;
        if (violations.Count == 0)
        {
            sb.AppendLine(Strings.Cmd_NoRuleViolations);
        }
        else
        {
            sb.AppendLine(Strings.Cmd_ViolationsHeader);


            foreach (var violation in violations)
            {
                sb.AppendLine();
                sb.AppendLine(string.Format(Strings.Cmd_RuleTypeLine, violation.Rule.DisplayName));

                if (violation.ViolatingRelationships.Count == 0)
                {
                    // Metric and cycle rules have no relationships; the description carries the finding.
                    sb.AppendLine(violation.Description);
                }

                // Participants of a NOCYCLES violation: name and count identify the group in the
                // Cycles view of the application.
                foreach (var element in violation.CycleElements)
                {
                    sb.AppendLine(element.FullName);
                }

                foreach (var (element, value) in violation.ViolatingElements)
                {
                    sb.AppendLine($"{element.FullName}: {value}");
                    foreach (var location in element.SourceLocations)
                    {
                        sb.AppendLine($"  {location}");
                    }
                }

                foreach (var relationship in violation.ViolatingRelationships)
                {
                    var sourceElement = graph.Nodes.GetValueOrDefault(relationship.SourceId);
                    var targetElement = graph.Nodes.GetValueOrDefault(relationship.TargetId);

                    if (sourceElement == null || targetElement == null)
                    {
                        sb.AppendLine(Strings.Cmd_InvalidRelationship);
                        continue;
                    }

                    sb.AppendLine($"{sourceElement.FullName} -> {targetElement.FullName}");

                    foreach (var location in relationship.SourceLocations)
                    {
                        sb.AppendLine($"  {location.ToString()}");
                    }
                }
            }
        }

        return sb.ToString();
    }
}