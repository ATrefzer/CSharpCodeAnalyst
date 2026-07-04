using System.Text;
using CSharpCodeAnalyst.Analyzers.Resources;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

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
                sb.AppendLine(string.Format(Strings.Cmd_RuleTypeLine, violation.Rule));
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