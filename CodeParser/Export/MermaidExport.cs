using System.Text;
using Contracts.Graph;

namespace CodeParser.Export;

/// <summary>
///     Exports a CodeGraph to Mermaid syntax.
/// </summary>
public class MermaidExport
{
    private readonly Dictionary<string, string> _nodeIdMap = new();
    private int _nodeCounter;

    public string Export(CodeGraph graph)
    {
        return ExportClass(graph);
    }

    /// <summary>
    ///     Exports the CodeGraph to Mermaid class diagram syntax and returns the result as a string.
    /// </summary>
    public string ExportClass(CodeGraph graph)
    {
        var builder = new StringBuilder();

        // Start with Mermaid class diagram directive

        var header = """
                     ---
                     config:
                       theme: 'base'
                       themeVariables:
                         primaryColor: '#ffffff'
                         primaryTextColor: '#000000'
                         primaryBorderColor: '#000000'
                         lineColor: '#000000'
                         secondaryColor: '#000000'
                         tertiaryColor: '#000000'
                     ---
                     """;

        builder.AppendLine(header);
        builder.AppendLine("classDiagram");
        builder.AppendLine();

        // Clear mapping for fresh export
        _nodeIdMap.Clear();
        _nodeCounter = 0;

        // Generate class definitions
        WriteClassDiagramNodes(builder, graph);
        builder.AppendLine();

        // Generate relationships for class diagram
        WriteClassDiagramRelationships(builder, graph);

        return builder.ToString();
    }


    private void WriteClassDiagramNodes(StringBuilder builder, CodeGraph graph)
    {
        // Only show classes, interfaces, structs, enums, records, delegates
        var typeNodes = graph.Nodes.Values
            .Where(n => IsClassDiagramType(n.ElementType))
            .ToList();

        foreach (var node in typeNodes)
        {
            WriteClassDefinition(builder, node, "    ");
        }
    }

    private void WriteClassDefinition(StringBuilder builder, CodeElement node, string indent)
    {
        var className = SanitizeClassName(node.Name);
        builder.AppendLine($"{indent}class {className} {{");

        // Add methods, properties, fields as class members
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

        // Add stereotype/annotation for specific types
        var stereotype = GetClassStereotype(node.ElementType);
        if (!string.IsNullOrEmpty(stereotype))
        {
            builder.AppendLine($"{indent}<<{stereotype}>> {className}");
        }
    }

    private void WriteClassDiagramRelationships(StringBuilder builder, CodeGraph graph)
    {
        var processedRelationships = new HashSet<string>();

        foreach (var node in graph.Nodes.Values.Where(n => IsClassDiagramType(n.ElementType)))
        {
            foreach (var relationship in node.Relationships)
            {
                // Ensure target node exists in the graph
                if (!graph.Nodes.TryGetValue(relationship.TargetId, out var targetNode))
                    continue;

                if (!IsClassDiagramType(targetNode.ElementType))
                    continue;

                var sourceClass = SanitizeClassName(node.Name);
                var targetClass = SanitizeClassName(targetNode.Name);
                var relationshipKey = $"{sourceClass}->{targetClass}:{relationship.Type}";

                if (!processedRelationships.Contains(relationshipKey))
                {
                    var arrow = GetClassDiagramArrow(relationship.Type);
                    if (!string.IsNullOrEmpty(arrow))
                    {
                        builder.AppendLine($"    {sourceClass} {arrow} {targetClass}");
                        processedRelationships.Add(relationshipKey);
                    }
                }
            }
        }
    }




    private void WriteNodeStyling(StringBuilder builder, CodeGraph graph)
    {
        // Group nodes by type for efficient styling
        var nodesByType = new Dictionary<CodeElementType, List<string>>();

        foreach (var node in graph.Nodes.Values)
        {
            if (!nodesByType.ContainsKey(node.ElementType))
            {
                nodesByType[node.ElementType] = new List<string>();
            }

            nodesByType[node.ElementType].Add(GetMermaidNodeId(node.Id));
        }

        // Create class definitions for each type
        foreach (var kvp in nodesByType)
        {
            var className = $"class{kvp.Key}";
            var nodeIds = string.Join(",", kvp.Value);
            builder.AppendLine($"    classDef {className} {GetNodeStyling(kvp.Key)};");
            builder.AppendLine($"    class {nodeIds} {className};");
        }
    }

    private string GetMermaidNodeId(string originalId)
    {
        if (!_nodeIdMap.ContainsKey(originalId))
        {
            _nodeIdMap[originalId] = $"N{_nodeCounter++}";
        }

        return _nodeIdMap[originalId];
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

    private static bool IsContainerType(CodeElementType elementType)
    {
        return elementType is CodeElementType.Assembly or
            CodeElementType.Namespace or
            CodeElementType.Class or
            CodeElementType.Interface or
            CodeElementType.Struct or
            CodeElementType.Record or
            CodeElementType.Enum or
            CodeElementType.Delegate;
    }

    private static string SanitizeClassName(string name)
    {
        // Remove generic type parameters and special characters for Mermaid class names
        return name.Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace(" ", "_");
    }

    private static string SanitizeLabel(string label)
    {
        // Escape quotes and handle special characters for Mermaid
        return label.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
    }

    private static string FormatClassMember(CodeElement member)
    {
        var symbol = member.ElementType switch
        {
            CodeElementType.Method => "+",
            CodeElementType.Property => "+",
            CodeElementType.Field => "-",
            CodeElementType.Event => "+",
            _ => "+"
        };

        var name = SanitizeLabel(member.Name);
        var suffix = member.ElementType switch
        {
            CodeElementType.Method => "()",
            _ => ""
        };

        return $"{symbol} {name}{suffix}";
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

    private static string GetClassDiagramArrow(RelationshipType relationshipType)
    {
        return relationshipType switch
        {
            RelationshipType.Inherits => "--|>", // Inheritance
            RelationshipType.Implements => "..|>", // Implementation
            RelationshipType.Uses => "-->", // Association/Dependency
            RelationshipType.Creates => "-->", // Association
            _ => "" // Skip other relationships in class diagram
        };
    }

    private static (string open, string close) GetNodeShape(CodeElementType elementType)
    {
        return elementType switch
        {
            CodeElementType.Assembly => ("[[", "]]"), // Subroutine shape
            CodeElementType.Namespace => ("[", "]"), // Rectangle
            CodeElementType.Class => ("[", "]"), // Rectangle  
            CodeElementType.Record => ("[", "]"), // Rectangle
            CodeElementType.Interface => ("{{", "}}"), // Hexagon
            CodeElementType.Struct => ("[", "]"), // Rectangle
            CodeElementType.Method => ("(", ")"), // Circle/Rounded
            CodeElementType.Property => ("(", ")"), // Circle/Rounded
            CodeElementType.Field => ("[", "]"), // Rectangle
            CodeElementType.Event => ("((", "))"), // Circle
            CodeElementType.Delegate => ("{{", "}}"), // Hexagon
            CodeElementType.Enum => ("[", "]"), // Rectangle
            _ => ("[", "]") // Default rectangle
        };
    }

    private static string GetArrowStyle(RelationshipType relationshipType)
    {
        return relationshipType switch
        {
            RelationshipType.Calls => "-->",
            RelationshipType.Invokes => "-->",
            RelationshipType.Creates => "-->",
            RelationshipType.Uses => "-.->", // Dotted arrow
            RelationshipType.Inherits => "==>", // Thick arrow
            RelationshipType.Implements => "==>", // Thick arrow
            RelationshipType.Overrides => "==>", // Thick arrow
            RelationshipType.Handles => "-->",
            RelationshipType.UsesAttribute => "-.->", // Dotted arrow
            _ => "-->"
        };
    }

    private static string GetRelationshipLabel(RelationshipType relationshipType)
    {
        return relationshipType switch
        {
            RelationshipType.Calls => "", // No label for calls (too verbose)
            RelationshipType.Invokes => "invokes",
            RelationshipType.Creates => "creates",
            RelationshipType.Uses => "uses",
            RelationshipType.Inherits => "inherits",
            RelationshipType.Implements => "implements",
            RelationshipType.Overrides => "overrides",
            RelationshipType.Handles => "handles",
            RelationshipType.UsesAttribute => "attr",
            _ => relationshipType.ToString().ToLowerInvariant()
        };
    }

    private static string GetNodeStyling(CodeElementType elementType)
    {
        return elementType switch
        {
            CodeElementType.Assembly => "fill:#EEEEEE,stroke:#333,stroke-width:2px",
            CodeElementType.Namespace => "fill:#4EC9B0,stroke:#333,stroke-width:1px",
            CodeElementType.Class => "fill:#FFD700,stroke:#333,stroke-width:1px",
            CodeElementType.Record => "fill:#FFD700,stroke:#333,stroke-width:1px",
            CodeElementType.Interface => "fill:#B8D7A3,stroke:#333,stroke-width:1px",
            CodeElementType.Struct => "fill:#FFA500,stroke:#333,stroke-width:1px",
            CodeElementType.Method => "fill:#569CD6,stroke:#333,stroke-width:1px",
            CodeElementType.Property => "fill:#4677a2,stroke:#333,stroke-width:1px",
            CodeElementType.Field => "fill:#D7BA7D,stroke:#333,stroke-width:1px",
            CodeElementType.Event => "fill:#FF69B4,stroke:#333,stroke-width:1px",
            CodeElementType.Delegate => "fill:#C586C0,stroke:#333,stroke-width:1px",
            CodeElementType.Enum => "fill:#9370DB,stroke:#333,stroke-width:1px",
            _ => "fill:#FFFFFF,stroke:#333,stroke-width:1px"
        };
    }
}