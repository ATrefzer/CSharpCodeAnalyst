using Contracts.Colors;
using Contracts.Graph;

namespace CodeParser.Export;

/// <summary>
///     Debug class to export the hierarchy of a code graph to a dgml file.
///     See <see cref="DgmlExport"/> for hierarchy and relationships.
/// </summary>
public class DgmlHierarchyExport
{
    public static void Export(string fileName, CodeGraph codeGraph)
    {
        var writer = new DgmlFileBuilder();

        var uniqueNodes = new HashSet<CodeElement>(codeGraph.Nodes.Values);


        WriteCategories(writer);
        WriteNodes(writer, uniqueNodes);
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
            foreach (var child in node.Children)
            {
                writer.AddEdgeById(node.Id, child.Id, "contains");
            }
        }
    }

    private static void WriteNodes(DgmlFileBuilder writer, IEnumerable<CodeElement> nodes)
    {
        foreach (var node in nodes)
        {
            writer.AddNodeById(node.Id, GetDgmlLabel(node), node.ElementType.ToString());
        }
    }

    private static string GetDgmlLabel(CodeElement node)
    {
        return node.Name;
    }
}