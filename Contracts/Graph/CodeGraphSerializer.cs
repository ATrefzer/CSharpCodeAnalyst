using System.Text;

namespace Contracts.Graph;

/// <summary>
///     Serializes and deserializes CodeGraph to/from a human-readable text format.
///     Format:
///     # Elements
///     ElementType Id [ name=Name] [ full=FullName] [ parent=ParentId] [ external] [ attr=Attr1,Attr2]
///     [loc=File:Line,Col]*
/// 
///     # Relationships
///     SourceId  RelType  TargetId [ Attr1,Attr2]
///     [loc=File:Line,Col]*
/// </summary>
public static class CodeGraphSerializer
{
    private const string ElementsHeader = "# Elements";
    private const string RelationshipsHeader = "# Relationships";
    private const string Separator = "  ";

    public static string Serialize(CodeGraph graph)
    {
        var sb = new StringBuilder();

        // Serialize elements
        sb.AppendLine(ElementsHeader);

        var sortedElements = GetSortedElements(graph);

        foreach (var element in sortedElements)
        {
            SerializeElement(sb, element);
        }

        sb.AppendLine();

        // Serialize relationships
        sb.AppendLine(RelationshipsHeader);

        // Any ordering is fine
        var relationships = graph.GetAllRelationships().OrderBy(r => GetRelationshipSortKey(graph, r));
        foreach (var rel in relationships)
        {
            SerializeRelationship(sb, rel);
        }

        return sb.ToString();
    }

    private static string GetRelationshipSortKey(CodeGraph graph, Relationship r)
    {
        return $"{graph.Nodes[r.SourceId].FullName}{r.Type.ToString()}{graph.Nodes[r.TargetId].FullName}";
    }

    public static void SerializeToFile(CodeGraph graph, string filePath)
    {
        var content = Serialize(graph);
        File.WriteAllText(filePath, content, Encoding.UTF8);
    }

    private static List<CodeElement> GetSortedElements(CodeGraph graph)
    {
        return graph.Nodes.Values.OrderBy(r => r.FullName).ToList();
    }

    private static void SerializeElement(StringBuilder sb, CodeElement element)
    {
        sb.Append($"{element.ElementType} {element.Id}");

        if (element.Name != element.Id)
        {
            sb.Append($" name={element.Name}");
        }

        if (element.FullName != element.Name)
        {
            sb.Append($" full={element.FullName}");
        }

        if (element.Parent != null)
        {
            sb.Append($"{Separator}parent={element.Parent.Id}");
        }

        if (element.IsExternal)
        {
            sb.Append($"{Separator}external");
        }

        if (element.Attributes.Count > 0)
        {
            var attrs = string.Join(",", element.Attributes.OrderBy(a => a));
            sb.Append($"{Separator}attr={attrs}");
        }

        if (element.SourceLocations.Count > 0)
        {
            SerializeSourceLocations(sb, element.SourceLocations);
        }

        sb.AppendLine();
    }

    private static void SerializeSourceLocations(StringBuilder sb, List<SourceLocation> locations)
    {
        foreach (var loc in locations)
        {
            sb.AppendLine();
            sb.Append($"loc={loc.ToString()}");
        }
    }

    private static void SerializeRelationship(StringBuilder sb, Relationship rel)
    {
        sb.Append($"{rel.SourceId} {rel.Type} {rel.TargetId}");
    
        if (rel.Attributes != RelationshipAttribute.None)
        {
            var attrs = GetAttributeFlags(rel.Attributes);
            sb.Append($" {string.Join(",", attrs)}");
        }
    
        if (rel.SourceLocations.Count > 0)
        {
            SerializeSourceLocations(sb, rel.SourceLocations);
        }
    
        sb.AppendLine();
    }

    private static List<string> GetAttributeFlags(RelationshipAttribute attr)
    {
        var flags = new List<string>();
        foreach (RelationshipAttribute flag in Enum.GetValues(typeof(RelationshipAttribute)))
        {
            if (flag != RelationshipAttribute.None && attr.HasFlag(flag))
            {
                flags.Add(flag.ToString());
            }
        }

        return flags;
    }

    public static CodeGraph Deserialize(string content)
    {
        var graph = new CodeGraph();
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd()).ToArray();

        var currentLine = 0;

        // Skip to Elements section
        while (currentLine < lines.Length && !lines[currentLine].StartsWith(ElementsHeader))
        {
            currentLine++;
        }

        if (currentLine >= lines.Length)
        {
            throw new InvalidOperationException("Elements header not found");
        }

        currentLine++; // Skip header

        // Parse elements - store parent IDs for later linking
        var parentIds = new Dictionary<string, string>(); // childId -> parentId

        while (currentLine < lines.Length)
        {
            var line = lines[currentLine].Trim();

            // Check if we reached relationships section
            if (line.StartsWith(RelationshipsHeader))
            {
                break;
            }

            // Parse element
            var (element, parentId, linesConsumed) = ParseElement(lines, currentLine);
            currentLine += linesConsumed;

            // Add to graph
            graph.Nodes[element.Id] = element;

            // Track parent ID for later linking
            if (parentId != null)
            {
                parentIds[element.Id] = parentId;
            }
        }

        // Link parent-child relationships in one place
        foreach (var (childId, parentId) in parentIds)
        {
            if (graph.Nodes.TryGetValue(childId, out var child) &&
                graph.Nodes.TryGetValue(parentId, out var parent))
            {
                child.Parent = parent;
                parent.Children.Add(child);
            }
        }

        // Skip relationships header
        currentLine++;

        // Parse relationships
        while (currentLine < lines.Length)
        {
            var line = lines[currentLine].Trim();

            // Parse relationship
            var (relationship, linesConsumed) = ParseRelationship(lines, currentLine);
            currentLine += linesConsumed;

            // Add relationship to source element
            if (graph.Nodes.TryGetValue(relationship.SourceId, out var sourceElement))
            {
                sourceElement.Relationships.Add(relationship);
            }
        }

        return graph;
    }

    public static CodeGraph DeserializeFromFile(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        return Deserialize(content);
    }

    private static (CodeElement element, string? parentId, int linesConsumed) ParseElement(string[] lines, int startLine)
    {
        var mainLine = lines[startLine];
        
        // Split arbitrary spaces
        var parts = mainLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            throw new InvalidOperationException($"Invalid element format at line {startLine}: {mainLine}");
        }

        var elementType = Enum.Parse<CodeElementType>(parts[0]);
        var id = parts[1];

        string? name = null;
        string? fullName = null;
        string? parentId = null;
        var isExternal = false;
        var attributes = new HashSet<string>();

        // Parse optional fields
        for (var i = 2; i < parts.Length; i++)
        {
            var part = parts[i];

            if (part.StartsWith("name="))
            {
                name = part.Substring("name=".Length);
            }
            else if (part.StartsWith("full="))
            {
                fullName = part.Substring("full=".Length);
            }
            else if (part.StartsWith("parent="))
            {
                parentId = part.Substring("parent=".Length);
            }
            else if (part == "external")
            {
                isExternal = true;
            }
            else if (part.StartsWith("attr="))
            {
                var attrList = part.Substring("attr=".Length).Split(',');
                foreach (var attr in attrList)
                {
                    attributes.Add(attr);
                }
            }
        }

        // Fallbacks for ooptional parameters
        name ??= id;
        fullName ??= name;

        // Create element without parent - will be linked later
        var element = new CodeElement(id, elementType, name, fullName, null)
        {
            IsExternal = isExternal
        };

        foreach (var attr in attributes)
        {
            element.Attributes.Add(attr);
        }

        // Parse source locations
        var linesConsumed = 1;
        var (sourceLocations, locLinesConsumed) = ParseSourceLocations(lines, startLine + 1);
        element.SourceLocations.AddRange(sourceLocations);
        linesConsumed += locLinesConsumed;

        return (element, parentId, linesConsumed);
    }

    private static (Relationship relationship, int linesConsumed) ParseRelationship(string[] lines, int startLine)
    {
        var mainLine = lines[startLine];
        var parts = mainLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    
        if (parts.Length < 3)
        {
            throw new InvalidOperationException($"Invalid relationship format at line {startLine}: {mainLine}");
        }
    
        var sourceId = parts[0];
        var relType = Enum.Parse<RelationshipType>(parts[1]);
        var targetId = parts[2];
    
        var attributes = RelationshipAttribute.None;
    
        // Parse optional attributes
        if (parts.Length > 3)
        {
            var attrList = parts[3].Split(',');
            foreach (var attr in attrList)
            {
                if (Enum.TryParse<RelationshipAttribute>(attr.Trim(), out var flag))
                {
                    attributes |= flag;
                }
            }
        }
    
        var relationship = new Relationship(sourceId, targetId, relType, attributes);
    
        // Parse source locations
        var linesConsumed = 1;
        var (sourceLocations, locLinesConsumed) = ParseSourceLocations(lines, startLine + 1);
        relationship.SourceLocations.AddRange(sourceLocations);
        linesConsumed += locLinesConsumed;
    
        return (relationship, linesConsumed);
    }

    private static SourceLocation ParseSourceLocation(string locString)
    {
        // Format: File:Line,Column
        var colonIndex = locString.LastIndexOf(':');
        if (colonIndex == -1)
        {
            throw new InvalidOperationException($"Invalid source location format: {locString}");
        }

        var file = locString.Substring(0, colonIndex);
        var lineColPart = locString.Substring(colonIndex + 1);

        var commaIndex = lineColPart.IndexOf(',');
        if (commaIndex == -1)
        {
            throw new InvalidOperationException($"Invalid source location format: {locString}");
        }

        var line = int.Parse(lineColPart.Substring(0, commaIndex));
        var column = int.Parse(lineColPart.Substring(commaIndex + 1));

        return new SourceLocation(file, line, column);
    }


    private static (List<SourceLocation> locations, int linesConsumed) ParseSourceLocations(string[] lines, int startLine)
    {
        var locations = new List<SourceLocation>();
        var linesConsumed = 0;
        var currentLine = startLine;

        while (currentLine < lines.Length)
        {
            var line = lines[currentLine].Trim();

            if (line.StartsWith("loc="))
            {
                var location = ParseSourceLocation(line.Substring("loc=".Length));
                locations.Add(location);
                linesConsumed++;
                currentLine++;
            }
            else
            {
                break;
            }
        }

        return (locations, linesConsumed);
    }
}