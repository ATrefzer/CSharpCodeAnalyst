using System.Xml.Linq;

namespace CodeGraph.Export;

public static class DsiExport
{
    public static void Export(string fileName, Graph.CodeGraph codeGraph)
    {
        var dsiXml = Convert(codeGraph);
        File.WriteAllText(fileName, dsiXml);
    }

    private static string Convert(Graph.CodeGraph codeGraph)
    {
        XNamespace ns = "urn:dsi-schema";

        var elements = new List<XElement>();
        var relations = new List<XElement>();
        var idMap = new Dictionary<string, int>();
        var currentId = 1;

        // Add code elements
        foreach (var node in codeGraph.Nodes.Values)
        {
            var fullName = node.GetFullPath();
            idMap[node.Id] = currentId;

            elements.Add(new XElement(ns + "element",
                new XAttribute("id", currentId),
                new XAttribute("name", fullName),
                new XAttribute("type", node.ElementType.ToString())
            ));

            currentId++;
        }

        // Add relationships (relation)
        foreach (var node in codeGraph.Nodes.Values)
        {
            foreach (var relationship in node.Relationships)
            {
                relations.Add(new XElement(ns + "relation",
                    new XAttribute("from", idMap[node.Id]),
                    new XAttribute("to", idMap[relationship.TargetId]),
                    new XAttribute("type", relationship.Type.ToString()),
                    new XAttribute("weight", "1")
                ));
            }
        }

        // Construct xml document
        var dsiModel = new XElement(ns + "dsimodel",
            new XAttribute("elementCount", elements.Count),
            new XAttribute("relationCount", relations.Count),
            new XElement(ns + "elements", elements),
            new XElement(ns + "relations", relations)
        );

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            dsiModel
        );

        return document.ToString();
    }
}