using System.Xml.Linq;
using CSharpCodeAnalyst.CodeGraph.Export;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Export;

/// <summary>
///     Element names carry the type parameters of a generic declaration ("Cache&lt;T&gt;"), which puts
///     characters into every output that some of them treat as syntax: angle brackets in XML and PlantUML
///     markup, the comma of a multi-parameter list in CSV, and the two together in a format that splits
///     its lines on whitespace. These are the invariants the exports have to keep, pinned here rather than
///     rediscovered the next time a name gains a character.
///     <para>
///         The pair "Cache" / "Cache&lt;T&gt;" is the interesting shape throughout: two elements that
///         used to be indistinguishable by name.
///     </para>
/// </summary>
[TestFixture]
public class GenericNamesInExportsTests
{
    private CodeGraph _graph = null!;
    private CodeElement _generic = null!;
    private CodeElement _plain = null!;

    [SetUp]
    public void SetUp()
    {
        // MyAsm.Store.Cache, MyAsm.Store.Cache<T> and MyAsm.Store.Map<TKey,TValue>.Add - the multi
        // parameter list brings the comma in, the method the second list along one path.
        _graph = new CodeGraph();
        var assembly = Add("asm", CodeElementType.Assembly, "MyAsm", null);
        var ns = Add("ns", CodeElementType.Namespace, "Store", assembly);
        _plain = Add("plain", CodeElementType.Class, "Cache", ns);
        _generic = Add("generic", CodeElementType.Class, "Cache<T>", ns);
        var map = Add("map", CodeElementType.Class, "Map<TKey,TValue>", ns);
        var add = Add("add", CodeElementType.Method, "Add<TResult>", map);

        _plain.Relationships.Add(new Relationship(_plain.Id, _generic.Id, RelationshipType.Inherits));
        add.Relationships.Add(new Relationship(add.Id, _generic.Id, RelationshipType.Uses));
    }

    [Test]
    public void FullNames_CarryTheTypeParametersAlongThePath()
    {
        // The premise of everything below: a type parameter list can sit anywhere in a full name, and
        // GetFullPath - which builds the path from the Names - agrees with the stored FullName.
        Assert.Multiple(() =>
        {
            Assert.That(_graph.Nodes["add"].FullName, Is.EqualTo("MyAsm.Store.Map<TKey,TValue>.Add<TResult>"));
            Assert.That(_graph.Nodes["add"].GetFullPath(), Is.EqualTo(_graph.Nodes["add"].FullName));
            Assert.That(_generic.FullName, Is.Not.EqualTo(_plain.FullName));
        });
    }

    [Test]
    public void PlainTextFormat_RoundTripsTheNames()
    {
        // The format splits its lines on whitespace, so a name may hold no space - "<TKey,TValue>" is
        // written from the parameter names alone for exactly this reason. A silent loss here would be
        // the worst case: the file still parses, with truncated names.
        var text = CodeGraphSerializer.Serialize(_graph);
        var restored = CodeGraphSerializer.Deserialize(text);

        Assert.Multiple(() =>
        {
            foreach (var original in _graph.Nodes.Values)
            {
                Assert.That(restored.Nodes[original.Id].Name, Is.EqualTo(original.Name));
                Assert.That(restored.Nodes[original.Id].FullName, Is.EqualTo(original.FullName));
            }
        });
    }

    [Test]
    public void PlantUml_GivesTheGenericAndNonGenericTypeDistinctAliases()
    {
        // The alias is the sanitized full name. While the two names were equal, both types collapsed
        // onto one alias and PlantUML merged two classes into one.
        var uml = new PlantUmlExport().Export(_graph);

        Assert.Multiple(() =>
        {
            Assert.That(uml, Contains.Substring("as MyAsm_Store_Cache {"));
            Assert.That(uml, Contains.Substring("as MyAsm_Store_Cache_T_ {"));
        });
    }

    [Test]
    public void PlantUml_WritesTheTypeParametersIntoTheLabel()
    {
        // The label is what a reader sees, so it spells the name out with its brackets. HTML entities
        // were tried and rejected: a quoted label does not resolve them, "&lt;T&gt;" renders literally.
        var uml = new PlantUmlExport().Export(_graph);

        Assert.Multiple(() =>
        {
            Assert.That(uml, Contains.Substring("class \"Cache<T>\" as MyAsm_Store_Cache_T_ {"));
            Assert.That(uml, Contains.Substring("class \"Map<TKey,TValue>\" as MyAsm_Store_Map_TKey_TValue_ {"));
            Assert.That(uml, Does.Not.Contain("&lt;"), "entities are not resolved in a label");

            // The alias is an identifier and has no escaping option - a raw bracket there is a syntax error.
            foreach (var line in uml.Split('\n').Where(l => l.Contains(" as ")))
            {
                Assert.That(line.Split(" as ")[1], Does.Not.Contain("<"), line.Trim());
            }
        });
    }

    [Test]
    public void Dgml_EscapesTheAngleBracketsAndKeepsTheLabel()
    {
        var file = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"generic-names-{Guid.NewGuid():N}.dgml");
        try
        {
            DgmlExport.Export(file, _graph);

            // Reading it back with an XML parser is the assertion: unescaped brackets would not survive.
            var labels = XDocument.Load(file).Descendants()
                .Where(e => e.Name.LocalName == "Node")
                .Select(e => e.Attribute("Label")?.Value)
                .ToList();

            Assert.That(labels, Does.Contain("Cache<T>").And.Contains("Map<TKey,TValue>"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Test]
    public void Dsi_EscapesTheAngleBracketsAndKeepsTheHierarchySeparator()
    {
        var file = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"generic-names-{Guid.NewGuid():N}.dsi");
        try
        {
            DsiExport.Export(file, _graph);

            var names = XDocument.Load(file).Descendants()
                .Where(e => e.Name.LocalName == "element")
                .Select(e => e.Attribute("name")?.Value ?? string.Empty)
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(names, Does.Contain("MyAsm.Store.Map<TKey,TValue>.Add<TResult>"));

                // The DSM viewer rebuilds the hierarchy by splitting these names on '.', so a type
                // parameter list must never contain one - it would invent hierarchy levels.
                foreach (var name in names)
                {
                    var insideBrackets = name.Split('<').Skip(1).Select(part => part.Split('>')[0]);
                    Assert.That(insideBrackets, Has.All.Not.Contains("."), $"'{name}' splits into extra levels");
                }
            });
        }
        finally
        {
            File.Delete(file);
        }
    }

    private CodeElement Add(string id, CodeElementType elementType, string name, CodeElement? parent)
    {
        var fullName = parent is null ? name : parent.FullName + "." + name;
        var element = new CodeElement(id, elementType, name, fullName, parent);
        parent?.Children.Add(element);
        _graph.Nodes[id] = element;
        return element;
    }
}
