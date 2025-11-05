using CodeGraph.Colors;
using CodeGraph.Graph;

namespace CodeGraph.Export;

/// <summary>
///     Debug class to export the relationship information of a code graph to a dgml file.
///     See <see cref="DgmlExport" /> for hierarchy and relationships.
/// </summary>
public class DgmlRelationshipExport
{
    public static void Export(string fileName, Graph.CodeGraph codeGraph)
    {
        var writer = new DgmlFileBuilder();

        var uniqueNodes = new HashSet<CodeElement>(codeGraph.Nodes.Values);


        WriteCategories(writer);
        WriteNodes(writer, uniqueNodes, codeGraph);
        WriteEdges(writer, uniqueNodes);

        writer.WriteOutput(fileName);
    }


    private static void WriteCategories(DgmlFileBuilder writer)
    {
        var elementTypes = Enum.GetValues(typeof(CodeElementType)).Cast<CodeElementType>();
        foreach (var type in elementTypes)
        {
            writer.AddCategory(type.ToString(), "Background", $"#{ColorDefinitions.GetRbgOf(type):X}");
        }
    }

    private static void WriteEdges(DgmlFileBuilder writer, IEnumerable<CodeElement> nodes)
    {
        foreach (var node in nodes)
        {
            foreach (var child in node.Relationships)
            {
                writer.AddEdgeById(node.Id, child.TargetId, child.Type.ToString());
            }
        }
    }


    private static void WriteNodes(DgmlFileBuilder writer, IEnumerable<CodeElement> nodes, Graph.CodeGraph codeGraph)
    {
        // Find all nodes we need for the graph.
        var allNodes = new HashSet<CodeElement>();
        foreach (var node in nodes.Where(n => n.Relationships.Count != 0))
        {
            allNodes.Add(node);
            foreach (var relationship in node.Relationships)
            {
                var targetElement = codeGraph.Nodes[relationship.TargetId];
                allNodes.Add(targetElement);
            }
        }

        foreach (var node in allNodes)
        {
            writer.AddNodeById(node.Id, GetDgmlLabel(node), node.ElementType.ToString());
        }
    }

    private static string GetDgmlLabel(CodeElement node)
    {
        return node.Name;
    }
}