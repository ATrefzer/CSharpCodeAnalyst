using System.Text;
using Contracts.Graph;

namespace CodeParser.Export;

/// <summary>
///     Exports a CodeGraph to PlantUML syntax.
/// </summary>
public class PlantUmlExport
{
    public string Export(CodeGraph graph)
    {
        return ExportClass(graph);
    }

    /// <summary>
    ///     Exports the CodeGraph to PlantUML class diagram syntax and returns the result as a string.
    /// </summary>
    private string ExportClass(CodeGraph graph)
    {
        var builder = new StringBuilder();

        // PlantUML diagram header
        builder.AppendLine("@startuml");
        builder.AppendLine("!theme plain");
        builder.AppendLine("hide footbox");
        builder.AppendLine("hide circle");
        builder.AppendLine("set namespaceSeparator .");

        //builder.AppendLine("skinparam linetype polyline");
        //builder.AppendLine("skinparam linetype ortho");


        builder.AppendLine();


        // Root containers: assemblies and namespaces
        var rootContainers = graph.Nodes.Values
            .Where(n => n.ElementType is CodeElementType.Assembly or CodeElementType.Namespace &&
                        n.Parent == null)
            .ToList();

        foreach (var container in rootContainers)
        {
            WriteContainerRecursive(builder, container, "");
        }

        // Types not inside any assembly/namespace
        var rootTypes = graph.Nodes.Values
            .Where(n => IsClassDiagramType(n.ElementType) &&
                        (n.Parent == null ||
                         !graph.Nodes.TryGetValue(n.Parent.Id, out var parent) ||
                         !(parent.ElementType == CodeElementType.Namespace ||
                           parent.ElementType == CodeElementType.Assembly)))
            .ToList();

        foreach (var typeNode in rootTypes)
        {
            WriteTypeDefinition(builder, typeNode, "");
        }

        builder.AppendLine();

        // Relationships
        WriteClassDiagramRelationships(builder, graph);

        builder.AppendLine("@enduml");
        return builder.ToString();
    }

    // Recursive writer for assemblies and namespaces
    private static void WriteContainerRecursive(StringBuilder builder, CodeElement containerNode, string indent)
    {
        var containerName = SanitizeClassName(containerNode.Name);

        var keyword = containerNode.ElementType switch
        {
            CodeElementType.Assembly => "package",
            CodeElementType.Namespace => "namespace",
            _ => "package"
        };

        builder.AppendLine($"{indent}{keyword} {containerName} {{");

        // Write child assemblies
        foreach (var childAsm in containerNode.Children.Where(c => c.ElementType == CodeElementType.Assembly))
        {
            WriteContainerRecursive(builder, childAsm, indent + "    ");
        }

        // Write child namespaces
        foreach (var childNs in containerNode.Children.Where(c => c.ElementType == CodeElementType.Namespace))
        {
            WriteContainerRecursive(builder, childNs, indent + "    ");
        }

        // Write contained types
        foreach (var typeNode in containerNode.Children.Where(c => IsClassDiagramType(c.ElementType)))
        {
            WriteTypeDefinition(builder, typeNode, indent + "    ");
        }

        builder.AppendLine($"{indent}}}");
    }

    private static void WriteTypeDefinition(StringBuilder builder, CodeElement node, string indent)
    {
        var className = SanitizeClassName(node.Name);
        builder.AppendLine($"{indent}class {className} {{");

        // Add class members (no visibility symbols)
        var members = node.Children
            .Where(c => IsMemberType(c.ElementType))
            .OrderBy(c => c.ElementType)
            .ThenBy(c => c.Name);

        foreach (var member in members)
        {
            var memberLine = FormatClassMember(member);
            builder.AppendLine($"{indent}    {memberLine}");
        }

        builder.AppendLine($"{indent}}}");

        // Stereotype for specific types
        var stereotype = GetClassStereotype(node.ElementType);
        if (!string.IsNullOrEmpty(stereotype))
        {
            builder.AppendLine($"{indent}{className} <<{stereotype}>>");
        }
    }

    /// <summary>
    ///     Writes relationships in class diagram.
    ///     While inheritance and implements are defined between types the dependencies are
    ///     calculated. <see cref="CalculateOutgoingTypeDependencies" />
    /// </summary>
    private static void WriteClassDiagramRelationships(StringBuilder builder, CodeGraph graph)
    {
        var processedRelationships = new HashSet<string>();
        var typeNodes = graph.Nodes.Values.Where(n => IsClassDiagramType(n.ElementType)).ToList();

        // Inheritance, implements
        foreach (var node in typeNodes)
        {
            foreach (var relationship in node.Relationships)
            {
                if (!graph.Nodes.TryGetValue(relationship.TargetId, out var targetNode)) continue;
                if (!IsClassDiagramType(targetNode.ElementType)) continue;

                var sourceClass = SanitizeClassName(node.GetFullPath());
                var targetClass = SanitizeClassName(targetNode.GetFullPath());

                var edge = relationship.Type switch
                {
                    RelationshipType.Inherits => $"{sourceClass} --|> {targetClass}",
                    RelationshipType.Implements => $"{sourceClass} ..|> {targetClass}",
                    _ => null
                };

                if (edge != null && processedRelationships.Add($"{sourceClass}:{targetClass}:{relationship.Type}"))
                {
                    builder.AppendLine($"    {edge}");
                }
            }
        }

        var allRelationships = graph.GetAllRelationships().ToHashSet();

        // Dependencies
        foreach (var node in typeNodes)
        {
            var dependencies = CalculateOutgoingTypeDependencies(node, typeNodes, allRelationships);

            foreach (var depId in dependencies)
            {
                if (!graph.Nodes.TryGetValue(depId, out var targetNode)) continue;
                if (!IsClassDiagramType(targetNode.ElementType)) continue;

                // We need the full path for the dependencies so we do not confuse 
                // namespaces and classes with same names.
                var sourceClass = SanitizeClassName(node.GetFullPath());
                var targetClass = SanitizeClassName(targetNode.GetFullPath());

                var key = $"{sourceClass}:{targetClass}:Uses";
                if (processedRelationships.Contains($"{sourceClass}:{targetClass}:Inherits") ||
                    processedRelationships.Contains($"{sourceClass}:{targetClass}:Implements") ||
                    !processedRelationships.Add(key))
                    continue;

                //                builder.AppendLine($"    {sourceClass} --> {targetClass} : depends");
                builder.AppendLine($"    {sourceClass} --> {targetClass}");
            }
        }
    }

    /// <summary>
    ///     Returns target ids
    /// </summary>
    private static HashSet<string> CalculateOutgoingTypeDependencies(CodeElement sourceType, List<CodeElement> typeNodes, HashSet<Relationship> allRelationships)
    {
        var dependencies = new HashSet<string>();
        var sourceCluster = sourceType.GetChildrenIncludingSelf();

        foreach (var targetType in typeNodes)
        {
            if (ReferenceEquals(sourceType, targetType))
            {
                if (allRelationships.Any(r =>
                        sourceCluster.Contains(r.SourceId) && r.TargetId == targetType.Id && r.Type == RelationshipType.Uses))
                {
                    // Self edge. I.e. composite pattern
                    dependencies.Add(targetType.Id);
                }

                continue;
            }

            var targetCluster = targetType.GetChildrenIncludingSelf();

            if (allRelationships.Any(r =>
                    sourceCluster.Contains(r.SourceId) && targetCluster.Contains(r.TargetId) && r.Type == RelationshipType.Uses))
            {
                dependencies.Add(targetType.Id);
            }
        }


        return dependencies;
    }


    private static bool IsClassDiagramType(CodeElementType elementType)
    {
        return elementType is CodeElementType.Class or
            CodeElementType.Interface or
            CodeElementType.Struct or
            CodeElementType.Enum or
            CodeElementType.Record or
            CodeElementType.Delegate;
    }

    private static bool IsMemberType(CodeElementType elementType)
    {
        return elementType is CodeElementType.Method or
            CodeElementType.Property or
            CodeElementType.Field or
            CodeElementType.Event;
    }

    private static string SanitizeClassName(string name)
    {
        return name.Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace(" ", "_");
    }

    private static string SanitizeLabel(string label)
    {
        return label.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
    }

    private static string FormatClassMember(CodeElement member)
    {
        var name = SanitizeLabel(member.Name);
        var suffix = member.ElementType switch
        {
            CodeElementType.Method => "()",
            _ => ""
        };
        return $"{name}{suffix}";
    }

    private static string GetClassStereotype(CodeElementType elementType)
    {
        return elementType switch
        {
            CodeElementType.Interface => "interface",
            CodeElementType.Struct => "struct",
            CodeElementType.Enum => "enumeration",
            CodeElementType.Record => "record",
            CodeElementType.Delegate => "delegate",
            _ => ""
        };
    }
}