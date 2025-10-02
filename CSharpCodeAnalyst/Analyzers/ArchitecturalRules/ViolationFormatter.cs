using System.Text;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

public class ViolationsFormatter
{
    public static string Format(CodeGraph graph, List<Violation> violations)
    {
        var sb = new StringBuilder();

        if (violations.Count == 0)
        {
            sb.AppendLine("No rule violations found.");
        }
        else
        {
            sb.AppendLine("Violations");
                

            foreach (var violation in violations)
            {
                sb.AppendLine();
                sb.AppendLine($"- Rule Type: {violation.Rule}");
                foreach (var relationship in violation.ViolatingRelationships)
                {
                    var sourceElement = graph.Nodes.GetValueOrDefault(relationship.SourceId);
                    var targetElement = graph.Nodes.GetValueOrDefault(relationship.TargetId);
                        
                    if (sourceElement == null || targetElement == null)
                    {
                        sb.AppendLine("(!) Invalid relationship with missing elements.");
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