using System.Text;
using CodeGraph.Graph;

namespace CodeGraph.Export;

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
    public string Export(Graph.CodeGraph graph)
    {
        return ExportClass(graph);
    }

    /// <summary>
    ///     Exports the CodeGraph to PlantUML class diagram syntax and returns the result as a string.
    /// </summary>
    private string ExportClass(Graph.CodeGraph graph)
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
        var containerName = SanitizeName(containerNode.Name, false);

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
        var typeDisplayName = SanitizeName(node.Name, false);
        var alias = SanitizeName(node.FullName, true);

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
    ///     calculated.
    /// </summary>
    private static void WriteClassDiagramRelationships(StringBuilder builder, Graph.CodeGraph graph)
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
            var sourceAlias = SanitizeName(sourceNode.FullName, true);
            var targetAlias = SanitizeName(targetNode.FullName, true);
            WriteUmlArrow(builder, sourceAlias, targetAlias, umlArrowType);
        }

        var other = orderedDependencies.Where(d => !IsInheritsOrImplements(d));
        var processedRelationships = new HashSet<(string, string)>();
        foreach (var (sourceNode, targetNode, umlArrowType) in other)
        {
            if (!IsClassDiagramType(targetNode.ElementType)) continue;

            // We need the full path for the dependencies so we do not confuse 
            // namespaces and classes with same names.
            var sourceAlias = SanitizeName(sourceNode.FullName, true);
            var targetAlias = SanitizeName(targetNode.FullName, true);

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

    /// <summary>
    ///     Aggregates the element-level relationships into one arrow per pair of class-diagram types,
    ///     keeping the strongest arrow (Inherits &gt; Implements &gt; Association &gt; weak dependency).
    ///     Single pass over the relationships (plus an element-to-owning-types index) 
    /// </summary>
    private static List<Dependency> CalculateUmlArrows(Graph.CodeGraph graph, List<CodeElement> typeNodes, HashSet<Relationship> allRelationships)
    {
        // Map every element to the class-diagram types that own it (itself and any enclosing types -
        // this mirrors the old "cluster = type.GetChildrenIncludingSelf()". Usually exactly one entry,
        // more only for nested types).
        var ownerTypes = BuildOwnerTypeIndex(typeNodes);

        // Strongest arrow per (sourceTypeId, targetTypeId) pair.
        var best = new Dictionary<(string Source, string Target), Dependency>();

        foreach (var relationship in allRelationships)
        {
            if (relationship.Type is RelationshipType.Bundled or RelationshipType.Containment)
            {
                continue;
            }

            if (!ownerTypes.TryGetValue(relationship.SourceId, out var sourceOwners) ||
                !ownerTypes.TryGetValue(relationship.TargetId, out var targetOwners))
            {
                continue;
            }

            var sourceIsField = graph.Nodes[relationship.SourceId].ElementType == CodeElementType.Field;

            foreach (var sourceType in sourceOwners)
            {
                foreach (var targetType in targetOwners)
                {
                    var arrow = ClassifyArrow(relationship, sourceType, targetType, sourceIsField);
                    if (arrow is null)
                    {
                        continue;
                    }

                    var key = (sourceType.Id, targetType.Id);
                    if (!best.TryGetValue(key, out var existing) || arrow.Value > existing.Type)
                    {
                        best[key] = new Dependency(sourceType, targetType, arrow.Value);
                    }
                }
            }
        }

        // First process the stronger relationships, then the weak ones
        return best.Values.OrderByDescending(d => d.Type).ToList();
    }

    /// <summary>
    ///     Classifies a single relationship into the UML arrow between two candidate owner types, or null
    ///     if it does not contribute. Mirrors the original priority: type-level Inherits/Implements, then a
    ///     field association to the target type, then a weak dependency for any other call/use.
    /// </summary>
    private static UmlArrowType? ClassifyArrow(Relationship relationship, CodeElement sourceType,
        CodeElement targetType, bool sourceIsField)
    {
        // Type-level inheritance/implementation: the type nodes themselves are the endpoints.
        // Note self was added to the ownerType list.
        if (relationship.SourceId == sourceType.Id && relationship.TargetId == targetType.Id)
        {
            if (relationship.Type == RelationshipType.Implements)
            {
                return UmlArrowType.Implements;
            }

            if (relationship.Type == RelationshipType.Inherits)
            {
                return UmlArrowType.Inherits;
            }
        }

        // Association: a field points at the target type.
        if (sourceIsField && relationship.TargetId == targetType.Id)
        {
            return UmlArrowType.DirectedAssociation;
        }

        // Any other call/use between members of two different types.
        if (relationship.Type is RelationshipType.Calls or RelationshipType.Uses && sourceType.Id != targetType.Id)
        {
            return UmlArrowType.WeakDependency;
        }

        return null;
    }

    /// <summary>
    ///     Builds the element-id -> owning class-diagram types index. A type owns itself and every element
    ///     in its subtree (including the members of nested types), matching the former cluster definition.
    /// </summary>
    private static Dictionary<string, List<CodeElement>> BuildOwnerTypeIndex(List<CodeElement> typeNodes)
    {
        var index = new Dictionary<string, List<CodeElement>>();
        foreach (var typeNode in typeNodes)
        {
            foreach (var memberId in typeNode.GetChildrenIncludingSelf())
            {
                if (!index.TryGetValue(memberId, out var owners))
                {
                    owners = [];
                    index[memberId] = owners;
                }

                owners.Add(typeNode);
            }
        }

        return index;
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

    private static string SanitizeName(string fullPath, bool replaceNamespaceSeparator)
    {
        var sanitized = fullPath.Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace(" ", "_").Replace("-", "_");
        if (replaceNamespaceSeparator)
        {
            // We do not see the alias, but it must not contain the namespace separator!
            // Otherwise, plantuml builds the namespace hierarchy from the class name.
            // Note: An alias does also not support underscores in the name but this is handled above.
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