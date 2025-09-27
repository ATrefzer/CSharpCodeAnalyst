using System.Xml;

namespace CodeParser.Export;

/// <summary>
///     Builder class to create a directed graph file to be processed with Visual Studio's
///     DGML viewer.
///     https://learn.microsoft.com/de-de/visualstudio/modeling/directed-graph-markup-language-dgml-reference?view=vs-2022
///     Example
///     <![CDATA[
///  <?xml version="1.0" encoding="utf-8"?>
///  <DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">
/// 	    <Categories>
/// 		    <Category Id="indirect" StrokeDashArray="1 1"/>
///  	</Categories>
/// 	    <Nodes>
///          <Node Id="0" Label="A"/>
///  		<Node Id="1" Label="B"/>		
/// 		    <Node Id="1" Label="C"/>
/// 	    </Nodes>
/// 	    <Links>
/// 		    <Link Source="0" Target="1" Category="indirect" />
/// 		    <Link Source="0" Target="2"/>	
/// 	    </Links>
/// </DirectedGraph>
///  ]]>
///     Groups are handled as normal nodes but:
///     - The node has the attribute Group="Expanded" or Group="Collapsed"
///     - There must be a link with Category="Contains"
///     <![CDATA[
///  <?xml version="1.0" encoding="utf-8"?>
///  <DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">
/// 	    <Categories>
/// 		    <Category Id="indirect" StrokeDashArray="1 1"/>
///  	</Categories>
/// 	    <Nodes>
///          <Node Id="Group_0" Label="Group" Group="Expanded"/>
///  		<Node Id="1" Label="B"/>		
/// 		    <Node Id="2" Label="C"/>
/// 	    </Nodes>
/// 	    <Links>
/// 		    <Link Source="0" Target="1" Category="indirect" />
/// 		    <Link Source="0" Target="2"/>	
/// 		    <Link Source="Group_0" Target="2" Category="Contains"/>	
/// 	    </Links>
/// </DirectedGraph>
/// 
///  ]]>
///     Example
///     <![CDATA[
///  var builder = new DgmlFileBuilder();
/// 
///  // Add nodes
///  var nodeA = builder.AddNodeById("A", "1");
///  var nodeB = builder.AddNodeById("B", "2");
///  var nodeC = builder.AddNodeById("C", "3");
///  var nodeD = builder.AddNodeById("D", "4");
///  
///  // Add groups
///  var group1 = builder.AddGroup("Group1", "Group 1");
///  var group2 = builder.AddGroup("Group2", "Group 2");
///  
///  // Add nodes to groups
///  builder.AddNodeToGroup("1", "Group1");
///  builder.AddNodeToGroup("2", "Group1");
///  builder.AddNodeToGroup("3", "Group2");
///  
///  // Add group to group (nested subgraph)
///  builder.AddGroupToGroup("Group2", "Group1");
///  
///  // Add edges
///  builder.AddEdgeById("1", "2", "Edge A-B");
///  builder.AddEdgeById("1", "3", "Edge A-C");
///  builder.AddEdgeById("2", "4", "Edge B-D");
///  
///  // Write the output
///  builder.WriteOutput("output.dgml");
///  ]]>
/// </summary>
public class DgmlFileBuilder
{
    private readonly Dictionary<string, Dictionary<string, string>> _categories = new();

    private readonly List<Edge> _edges = [];

    private readonly Dictionary<string, Group> _groups = new();

    private readonly Dictionary<string, Node> _nodes = new();

    /// <summary>
    ///     Adds a category, for example AddCategory("HotTemperature", "Background" "Red")
    /// </summary>
    public void AddCategory(string category, string property, string value)
    {
        if (!_categories.TryGetValue(category, out var properties))
        {
            properties = new Dictionary<string, string>();
            _categories.Add(category, properties);
        }

        properties[property] = value;
    }

    public void AddEdgeById(string sourceId, string targetId, string label)
    {
        _edges.Add(new Edge(sourceId, targetId, label));
    }

    public Group AddGroup(string groupId, string label, string category)
    {
        var group = new Group(groupId, label, category);
        _groups.Add(groupId, group);
        return group;
    }

    public void AddGroupToGroup(string parentGroupId, string childGroupId)
    {
        if (_groups.TryGetValue(parentGroupId, out var parentGroup))
        {
            parentGroup.ChildGroupIds.Add(childGroupId);
        }
        else
        {
            throw new ArgumentException($"Parent group with id {parentGroupId} not found.");
        }
    }

    public Node AddNodeById(string id, string nodeName)
    {
        var node = new Node(id, nodeName);
        _nodes.Add(id, node);
        return node;
    }

    public Node AddNodeById(string id, string nodeName, string category)
    {
        var node = new Node(id, nodeName)
        {
            Category = category
        };
        _nodes.Add(id, node);
        return node;
    }

    public void AddNodeToGroup(string groupId, string nodeId)
    {
        if (_groups.TryGetValue(groupId, out var group))
        {
            group.NodeIds.Add(nodeId);
        }
        else
        {
            throw new ArgumentException($"Group with id {groupId} not found.");
        }
    }

    /// <summary>
    ///     Creates the output file.
    /// </summary>
    public void WriteOutput(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using var writer = XmlWriter.Create(path);

        writer.WriteStartDocument();
        writer.WriteStartElement("DirectedGraph", "http://schemas.microsoft.com/vs/2009/dgml");

        WriteCategories(writer);
        writer.WriteStartElement("Nodes");
        WriteNodes(writer);

        writer.WriteEndElement(); // Nodes
        WriteEdges(writer);

        writer.WriteEndElement(); // DirectedGraph
        writer.WriteEndDocument();
    }

    private static bool IsNonPrintableCharacter(char candidate)
    {
        return candidate < 0x20 || candidate > 127;
    }

    private static void WriteGroupNode(XmlWriter writer, Group group)
    {
        writer.WriteStartElement("Node");
        writer.WriteAttributeString("Id", group.Id);
        writer.WriteAttributeString("Label", group.Label);
        writer.WriteAttributeString("Group", "Expanded");
        if (group.HasCategory)
        {
            writer.WriteAttributeString("Category", group.Category);
        }

        writer.WriteEndElement(); // Node (Group)
    }

    private static void WriteNode(XmlWriter writer, Node node)
    {
        var id = node.Id;
        writer.WriteStartElement("Node");
        writer.WriteAttributeString("Id", id);

        var escaped = node.Name;
        if (node.Name.Any(IsNonPrintableCharacter))
        {
            escaped = "Cryptic_" + node.Name;
        }

        writer.WriteAttributeString("Label", escaped);

        if (node.HasCategory)
        {
            writer.WriteAttributeString("Category", node.Category);
        }

        if (node.HasTooltip)
        {
            writer.WriteAttributeString("Tooltip", node.Tooltip);
        }

        writer.WriteEndElement();
    }

    private void WriteCategories(XmlWriter writer)
    {
        writer.WriteStartElement("Categories");
        foreach (var category in _categories)
        {
            if (!category.Value.Any())
            {
                continue;
            }

            writer.WriteStartElement("Category");
            writer.WriteAttributeString("Id", category.Key);
            foreach (var property in category.Value)
            {
                writer.WriteAttributeString(property.Key, property.Value);
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private void WriteEdges(XmlWriter writer)
    {
        writer.WriteStartElement("Links");
        foreach (var edge in _edges)
        {
            writer.WriteStartElement("Link");
            writer.WriteAttributeString("Source", edge.Source);
            writer.WriteAttributeString("Target", edge.Target);
            if (!string.IsNullOrEmpty(edge.Category))
            {
                writer.WriteAttributeString("Category", edge.Category);
            }

            if (!string.IsNullOrEmpty(edge.Label))
            {
                writer.WriteAttributeString("Label", edge.Label);
            }

            writer.WriteEndElement();
        }

        // Group contains relationships
        foreach (var group in _groups)
        {
            foreach (var childNodeId in group.Value.NodeIds)
            {
                writer.WriteStartElement("Link");
                writer.WriteAttributeString("Source", group.Key);
                writer.WriteAttributeString("Target", childNodeId);
                writer.WriteAttributeString("Category", "Contains");
                writer.WriteEndElement();
            }

            foreach (var subGroupId in group.Value.ChildGroupIds)
            {
                writer.WriteStartElement("Link");
                writer.WriteAttributeString("Source", group.Key);
                writer.WriteAttributeString("Target", subGroupId);
                writer.WriteAttributeString("Category", "Contains");
                writer.WriteEndElement();
            }
        }

        writer.WriteEndElement(); // Links
    }

    private void WriteNodes(XmlWriter writer)
    {
        foreach (var node in _nodes.Values)
        {
            WriteNode(writer, node);
        }

        // Just normal nodes but tagged with Group="Expanded"
        foreach (var group in _groups.Values)
        {
            WriteGroupNode(writer, group);
        }
    }

    public class Node(string id, string name)
    {
        public string Category { get; set; } = "";

        public bool HasCategory
        {
            get => !string.IsNullOrEmpty(Category);
        }

        public bool HasTooltip
        {
            get => !string.IsNullOrEmpty(Tooltip);
        }

        public string Id { get; set; } = id;

        public string Name { get; set; } = name;

        public string Tooltip { get; set; } = "";

        public Node WithCategory(string category)
        {
            Category = category;
            return this;
        }

        public Node WithTooltip(string tooltip)
        {
            Tooltip = tooltip;
            return this;
        }
    }

    private class Edge
    {
        public Edge(string sourceNode, string targetNode, string category, string label)
        {
            Source = sourceNode;
            Target = targetNode;
            Category = category;
            Label = label;
        }

        public Edge(string sourceNode, string targetNode, string label)
        {
            Source = sourceNode;
            Target = targetNode;
            Category = "";
            Label = label;
        }

        public string Category { get; }
        public string Label { get; set; }
        public string Source { get; }
        public string Target { get; }
    }
}

public class Group(string id, string label, string category)
{
    public List<string> ChildGroupIds { get; } = [];
    public string Id { get; set; } = id;
    public string Label { get; set; } = label;
    public string Category { get; set; } = category;
    public List<string> NodeIds { get; } = [];

    public bool HasCategory
    {
        get => !string.IsNullOrEmpty(Category);
    }
}