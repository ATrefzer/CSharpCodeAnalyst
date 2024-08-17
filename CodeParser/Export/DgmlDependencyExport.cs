using Contracts.Colors;
using Contracts.Graph;

namespace CodeParser.Export;

public class DgmlDependencyExport
{
    public static void Export(string fileName, CodeGraph codeGraph)
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
            foreach (var child in node.Dependencies)
            {
                writer.AddEdgeById(node.Id, child.TargetId, child.Type.ToString());
            }
        }
    }


    private static void WriteNodes(DgmlFileBuilder writer, IEnumerable<CodeElement> nodes, CodeGraph codeGraph)
    {
        // Find all nodes we need for the graph.
        var allNodes = new HashSet<CodeElement>();
        foreach (var node in nodes.Where(n => n.Dependencies.Count != 0))
        {
            allNodes.Add(node);
            foreach (var dependency in node.Dependencies)
            {
                var targetElement = codeGraph.Nodes[dependency.TargetId];
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