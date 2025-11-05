using System.Text;
using CodeGraph.Export;
using CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Export;

[TestFixture]
public class CodeGraphSerializerTests
{
    private CodeGraph.Graph.CodeGraph CreateSampleGraph()
    {
        var g = new CodeGraph.Graph.CodeGraph();
        var asm = new CodeElement("Asm", CodeElementType.Assembly, "Asm", "Asm", null);
        var ns = new CodeElement("Ns", CodeElementType.Namespace, "Ns", "Ns", asm);
        var cls = new CodeElement("Cls", CodeElementType.Class, "Cls", "Ns.Cls", ns);
        var method = new CodeElement("M", CodeElementType.Method, "M", "Ns.Cls.M", cls);
        var field = new CodeElement("F", CodeElementType.Field, "F", "Ns.Cls.F", cls);

        asm.Children.Add(ns);
        ns.Children.Add(cls);
        cls.Children.Add(method);
        cls.Children.Add(field);
        g.Nodes[asm.Id] = asm;
        g.Nodes[ns.Id] = ns;
        g.Nodes[cls.Id] = cls;
        g.Nodes[method.Id] = method;
        g.Nodes[field.Id] = field;

        // attributes + external + locations
        cls.Attributes.Add("Serializable");
        method.SourceLocations.Add(new SourceLocation("file1.cs", 10, 5));
        method.SourceLocations.Add(new SourceLocation("file1.cs", 12, 15));
        field.IsExternal.Equals(false); // just keep reference

        // relationships
        var rel1 = new Relationship(method.Id, field.Id, RelationshipType.Uses, RelationshipAttribute.IsInstanceCall | RelationshipAttribute.IsMethodGroup);
        rel1.SourceLocations.Add(new SourceLocation("file1.cs", 20, 3));
        method.Relationships.Add(rel1);

        return g;
    }

    [Test]
    public void SerializeDeserialize_RoundTrip_MatchesStructure()
    {
        var g = CreateSampleGraph();
        var text = CodeGraphSerializer.Serialize(g);
        var restored = CodeGraphSerializer.Deserialize(text);

        Assert.That(restored.Nodes.Count, Is.EqualTo(g.Nodes.Count));

        foreach (var id in g.Nodes.Keys)
        {
            Assert.That(restored.Nodes.ContainsKey(id), $"Missing node {id}");
            var orig = g.Nodes[id];
            var copy = restored.Nodes[id];
            Assert.That(copy.ElementType, Is.EqualTo(orig.ElementType));
            Assert.That(copy.Name, Is.EqualTo(orig.Name));
            Assert.That(copy.FullName, Is.EqualTo(orig.FullName));
            Assert.That(copy.Parent?.Id, Is.EqualTo(orig.Parent?.Id));
            CollectionAssert.AreEquivalent(orig.Attributes, copy.Attributes);
            CollectionAssert.AreEquivalent(orig.SourceLocations, copy.SourceLocations);
        }

        // Relationship checks (only one in sample)
        var restoredMethod = restored.Nodes["M"];
        Assert.That(restoredMethod.Relationships.Count, Is.EqualTo(1));
        var r = restoredMethod.Relationships.First();
        Assert.That(r.SourceId, Is.EqualTo("M"));
        Assert.That(r.TargetId, Is.EqualTo("F"));
        Assert.That(r.Type, Is.EqualTo(RelationshipType.Uses));
        Assert.That(r.Attributes.HasFlag(RelationshipAttribute.IsInstanceCall));
        Assert.That(r.Attributes.HasFlag(RelationshipAttribute.IsMethodGroup));
        Assert.That(r.SourceLocations.Count, Is.EqualTo(1));
    }

    [Test]
    public void Deserialize_InvalidLine_Throws()
    {
        var invalid = "BogusLine"; // not enough parts
        Assert.Throws<InvalidOperationException>(() => CodeGraphSerializer.Deserialize(invalid));
    }

    [Test]
    public void Deserialize_BadLocationFormat_Throws()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Class A");
        sb.AppendLine("loc=fileonly-no-line");
        var txt = sb.ToString();
        Assert.Throws<InvalidOperationException>(() => CodeGraphSerializer.Deserialize(txt));
    }

    [Test]
    public void Serialize_DeterministicOrdering()
    {
        var g = CreateSampleGraph();
        var t1 = CodeGraphSerializer.Serialize(g);
        var t2 = CodeGraphSerializer.Serialize(g);
        Assert.That(t1, Is.EqualTo(t2));
    }
}