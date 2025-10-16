using System.Text;
using Contracts.Graph;

namespace CodeParser.Export;

/// <summary>
///     Exports a CodeGraph to PlantUML syntax.
///     Following arrows are implemented
///     - Type inheritance and interface realization
///     - Field references to other types are mapped to a directed association
///     - All other dependencies from source to target types (deep, like calls) are mapped to a weak dependency.
/// 
///     Note: PlantUML can have conflicts when a type name matches a namespace name
///     (e.g., namespace Export and class Export). To avoid this, we use aliases for all types.
///     The alias is the type's FullName with dots replaced by underscores to prevent
///     PlantUML from interpreting it as a namespace hierarchy.
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
        var containerName = SanitizeClassName(containerNode.Name, false);

        var keyword = containerNode.ElementType switch
        {
            CodeElementType.Assembly => "package",
            CodeElementType.Namespace => "namespace",
            _ => "package"
        };

        builder.AppendLine($"{indent}{keyword} {containerName} {{");

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
        var typeDisplayName = SanitizeClassName(node.Name, false);
        var alias = SanitizeClassName(node.FullName, true);

        // Always use alias syntax with full path as the identifier
        builder.AppendLine($"{indent}class \"{typeDisplayName}\" as {alias} {{");

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
            builder.AppendLine($"{indent}{alias} <<{stereotype}>>");
        }
    }

    /// <summary>
    ///     Writes relationships in class diagram.
    ///     While inheritance and implements are defined between types the dependencies are
    ///     calculated. <see cref="CalculateOutgoingTypeDependencies" />
    /// </summary>
    private static void WriteClassDiagramRelationships(StringBuilder builder, CodeGraph graph)
    {
        var typeNodes = graph.Nodes.Values.Where(n => IsClassDiagramType(n.ElementType)).ToList();
        var allRelationships = graph.GetAllRelationships().ToHashSet();
        var orderedDependencies = CalculateUmlArrows(graph, typeNodes, allRelationships);
        WriteUmlArrows(builder, orderedDependencies);
    }

    /// <summary>
    ///     If there is an association and a dependency between two class, only the stronger association is drawn
    /// </summary>
    private static void WriteUmlArrows(StringBuilder builder, List<Dependency> orderedDependencies)
    {
        var inheritance = orderedDependencies.Where(IsInheritsOrImplements).ToList();
        foreach (var (sourceNode, targetNode, umlArrowType) in inheritance)
        {
            if (!IsClassDiagramType(targetNode.ElementType)) continue;

            // This already uses the full path aka alias
            var sourceAlias = SanitizeClassName(sourceNode.FullName, true);
            var targetAlias = SanitizeClassName(targetNode.FullName, true);
            WriteUmlArrow(builder, sourceAlias, targetAlias, umlArrowType);
        }

        var other = orderedDependencies.Where(d => !IsInheritsOrImplements(d));
        var processedRelationships = new HashSet<(string, string)>();
        foreach (var (sourceNode, targetNode, umlArrowType) in other)
        {
            if (!IsClassDiagramType(targetNode.ElementType)) continue;

            // We need the full path for the dependencies so we do not confuse 
            // namespaces and classes with same names.
            var sourceAlias = SanitizeClassName(sourceNode.FullName, true);
            var targetAlias = SanitizeClassName(targetNode.FullName, true);

            var key = (sourceAlias, targetAlias);
            if (!processedRelationships.Add(key))
            {
                // Don't add a weak dependency when there is already an association
                continue;
            }

            WriteUmlArrow(builder, sourceAlias, targetAlias, umlArrowType);
        }

        bool IsInheritsOrImplements(Dependency d)
        {
            return d.Type is UmlArrowType.Inherits or UmlArrowType.Implements;
        }
    }

    private static void WriteUmlArrow(StringBuilder builder, string sourceClass, string targetClass,
        UmlArrowType umlArrowType)
    {
        if (umlArrowType == UmlArrowType.Inherits)
        {
            builder.AppendLine($"    {sourceClass} --|> {targetClass}");
        }
        else if (umlArrowType == UmlArrowType.Implements)
        {
            builder.AppendLine($"    {sourceClass} ..|> {targetClass}");
        }
        else if (umlArrowType == UmlArrowType.DirectedAssociation)
        {
            builder.AppendLine($"    {sourceClass} --> {targetClass}");
        }
        else if (umlArrowType == UmlArrowType.WeakDependency)
        {
            builder.AppendLine($"    {sourceClass} ..> {targetClass}");
            //builder.AppendLine($"    {sourceClass} --> {targetClass} : depends");
        }
    }

    private static List<Dependency> CalculateUmlArrows(CodeGraph graph, List<CodeElement> typeNodes, HashSet<Relationship> allRelationships)
    {
        HashSet<Dependency> dependencies = [];
        foreach (var sourceNode in typeNodes)
        {
            dependencies.UnionWith(CalculateOutgoingTypeDependencies(graph, sourceNode, typeNodes, allRelationships));
        }

        // First process the stronger relationships, then the weak ones
        var orderedDependencies = dependencies.OrderByDescending(d => d.Type).ToList();
        return orderedDependencies;
    }


    private static HashSet<Dependency> CalculateOutgoingTypeDependencies(CodeGraph graph, CodeElement sourceType, List<CodeElement> typeNodes, HashSet<Relationship> relationships)
    {
        // Prefilter noise and already processed relationships
        var allRelationships = relationships.Where(r =>
            r.Type != RelationshipType.Bundled
            && r.Type != RelationshipType.Containment).ToList();


        var dependencies = new HashSet<Dependency>();

        foreach (var targetType in typeNodes)
        {
            if (allRelationships.Any(r =>
                    r.Type == RelationshipType.Implements &&
                    sourceType.Id == r.SourceId &&
                    targetType.Id == r.TargetId))
            {
                dependencies.Add(new Dependency(sourceType, targetType, UmlArrowType.Implements));
                continue;
            }

            if (allRelationships.Any(r =>
                    r.Type == RelationshipType.Inherits &&
                    sourceType.Id == r.SourceId &&
                    targetType.Id == r.TargetId))
            {
                dependencies.Add(new Dependency(sourceType, targetType, UmlArrowType.Inherits));
                continue;
            }

            var sourceCluster = sourceType.GetChildrenIncludingSelf().Select(t => graph.Nodes[t]).ToList();

            // Association to target class by field member
            if (allRelationships.Any(r =>
                    sourceCluster.Any(s => s.Id == r.SourceId && s.ElementType == CodeElementType.Field) &&
                    targetType.Id == r.TargetId))
            {
                dependencies.Add(new Dependency(sourceType, targetType, UmlArrowType.DirectedAssociation));
                continue;
            }

            // Any other (weak) dependencies (calls etc.)
            if (sourceType.Id == targetType.Id)
            {
                // No self edges for weak dependencies
                continue;
            }

            var targetCluster = targetType.GetChildrenIncludingSelf();
            var weakRelationships = allRelationships.Where(r =>
                r.Type is RelationshipType.Calls or RelationshipType.Uses &&
                sourceCluster.Any(s => s.Id == r.SourceId) &&
                targetCluster.Any(t => t == r.TargetId)).ToList();

            //var dbg = weakRelationships.Select(r => (graph.Nodes[r.SourceId], graph.Nodes[r.TargetId], r));

            if (weakRelationships.Any())
            {
                // Compress edges
                dependencies.Add(new Dependency(sourceType, targetType, UmlArrowType.WeakDependency));
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

    private static string SanitizeClassName(string fullPath, bool replaceNamespaceSeparator)
    {
        var sanitized = fullPath.Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace(" ", "_");
        if (replaceNamespaceSeparator)
        {
            // We do not see the alias, but it must not contain the namespace separator!
            // Otherwise, plantuml builds the namespace hierarchy from the class name.
            sanitized = sanitized.Replace(".", "_");
        }

        return sanitized;
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


    private enum UmlArrowType
    {
        WeakDependency,
        DirectedAssociation,
        Implements,
        Inherits
    }

    private record Dependency(CodeElement Source, CodeElement Target, UmlArrowType Type);
}